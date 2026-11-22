namespace PoeFilterX.Engine.Projects;

/// <summary>Supplies a fresh readable stream; the project loader owns and disposes it.</summary>
public interface IProjectSource
{
    Stream OpenRead(string absolutePath);
}
