using Elsa.Expressions.JavaScript.TypeDefinitions.Models;

namespace Elsa.Expressions.JavaScript.TypeDefinitions.Contracts;

/// <summary>
/// Provides <see cref="VariableDefinition"/>s to the type definition document being constructed.
/// </summary>
public interface IVariableDefinitionProvider
{
    ValueTask<IEnumerable<VariableDefinition>> GetVariableDefinitionsAsync(TypeDefinitionContext context);
}