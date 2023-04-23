namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IVariableStore
    {
        void Add(string key, string value);
        string[] InjectEnvironment(IReadOnlyList<string> args);
    }
}