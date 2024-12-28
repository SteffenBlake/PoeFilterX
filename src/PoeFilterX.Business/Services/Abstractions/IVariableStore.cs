namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IVariableStore
    {
        void Track(string key);
        void Add(string key, string value);
        string[] InjectEnvironment(IReadOnlyList<string> args);
    }
}