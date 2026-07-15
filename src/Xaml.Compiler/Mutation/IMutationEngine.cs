using System.Threading.Tasks;

namespace Xaml.Compiler.Mutation;

public interface IMutationEngine
{
    bool CanMutate(object target);
    Task<bool> SetAttributeAsync(object target, string name, string value);
    Task<bool> RemoveAttributeAsync(object target, string name);
    Task<bool> RemoveNodeAsync(object target);
    Task<bool> SetOuterHtmlAsync(object target, string outerHtml);
}
