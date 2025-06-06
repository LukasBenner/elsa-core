using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Acornima.Ast;
using Elsa.Expressions.Helpers;
using Elsa.Expressions.Models;
using Elsa.Expressions.JavaScript.Contracts;
using Elsa.Expressions.JavaScript.Helpers;
using Elsa.Expressions.JavaScript.Notifications;
using Elsa.Expressions.JavaScript.ObjectConverters;
using Elsa.Expressions.JavaScript.Options;
using Elsa.Mediator.Contracts;
using Jint;
using Jint.Runtime.Interop;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// ReSharper disable ConvertClosureToMethodGroup
namespace Elsa.Expressions.JavaScript.Services;

/// <summary>
/// Provides a JavaScript evaluator using Jint.
/// </summary>
public class JintJavaScriptEvaluator(IConfiguration configuration, INotificationSender mediator, IOptions<JintOptions> scriptOptions, IMemoryCache memoryCache)
    : IJavaScriptEvaluator
{
    private readonly JintOptions _jintOptions = scriptOptions.Value;

    /// <inheritdoc />
    [RequiresUnreferencedCode("The Jint library uses reflection and can't be statically analyzed.")]
    public async Task<object?> EvaluateAsync(string expression,
        Type returnType,
        ExpressionExecutionContext context,
        ExpressionEvaluatorOptions? options = null,
        Action<Engine>? configureEngine = null,
        CancellationToken cancellationToken = default)
    {
        var engine = await GetConfiguredEngine(configureEngine, context, options, cancellationToken);
        await mediator.SendAsync(new EvaluatingJavaScript(engine, context, expression), cancellationToken);
        var result = ExecuteExpressionAndGetResult(engine, expression);
        await mediator.SendAsync(new EvaluatedJavaScript(engine, context, expression, result), cancellationToken);

        return result.ConvertTo(returnType);
    }

    private async Task<Engine> GetConfiguredEngine(Action<Engine>? configureEngine, ExpressionExecutionContext context, ExpressionEvaluatorOptions? options, CancellationToken cancellationToken)
    {
        options ??= new();

        var engineOptions = new Jint.Options
        {
            ExperimentalFeatures = ExperimentalFeature.TaskInterop,
        };

        ConfigureClrAccess(engineOptions);
        ConfigureObjectWrapper(engineOptions);
        ConfigureObjectConverters(engineOptions);

        await mediator.SendAsync(new CreatingJavaScriptEngine(engineOptions, context), cancellationToken);
        _jintOptions.ConfigureEngineOptionsCallback(engineOptions, context);

        var engine = new Engine(engineOptions);

        configureEngine?.Invoke(engine);
        ConfigureArgumentGetters(engine, options);
        ConfigureConfigurationAccess(engine);
        _jintOptions.ConfigureEngineCallback(engine, context);

        return engine;
    }

    private void ConfigureClrAccess(Jint.Options options)
    {
        if (_jintOptions.AllowClrAccess)
            options.AllowClr();
    }

    private void ConfigureObjectWrapper(Jint.Options options)
    {
        options.SetWrapObjectHandler((engine, target, type) =>
        {
            var instance = ObjectWrapper.Create(engine, target);

            if (ObjectArrayHelper.DetermineIfObjectIsArrayLikeClrCollection(target.GetType()))
                instance.Prototype = engine.Intrinsics.Array.PrototypeObject;

            return instance;
        });
    }

    private void ConfigureObjectConverters(Jint.Options options)
    {
        options.Interop.ObjectConverters.AddRange([new ByteArrayConverter(), new EnumToStringConverter(), new JsonElementConverter()]);
    }

    private void ConfigureArgumentGetters(Engine engine, ExpressionEvaluatorOptions options)
    {
        foreach (var argument in options.Arguments)
            engine.SetValue($"get{argument.Key}", (Func<object?>)(() => argument.Value));
    }

    private void ConfigureConfigurationAccess(Engine engine)
    {
        if (_jintOptions.AllowConfigurationAccess)
            engine.SetValue("getConfig", (Func<string, object?>)(name => configuration.GetSection(name).Value));
    }

    private object? ExecuteExpressionAndGetResult(Engine engine, string expression)
    {
        var preparedScript = GetOrCreatePrepareScript(expression);
        var result = engine.Evaluate(preparedScript);
        return result.UnwrapIfPromise().ToObject();
    }

    private Prepared<Script> GetOrCreatePrepareScript(string expression)
    {
        var cacheKey = "jint:script:" + Hash(expression);

        return memoryCache.GetOrCreate(cacheKey, entry =>
        {
            if (_jintOptions.ScriptCacheTimeout.HasValue)
                entry.SetSlidingExpiration(_jintOptions.ScriptCacheTimeout.Value);

            return PrepareScript(expression);
        })!;
    }

    private Prepared<Script> PrepareScript(string expression)
    {
        var prepareOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new()
            {
                AllowReturnOutsideFunction = true
            }
        };
        return Engine.PrepareScript(expression, options: prepareOptions);
    }

    private string Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}