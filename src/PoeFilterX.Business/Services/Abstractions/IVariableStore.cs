namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IVariableStore : ISectionParser
    {
        string[] InjectEnvironment(IReadOnlyList<string> args);
    }
}