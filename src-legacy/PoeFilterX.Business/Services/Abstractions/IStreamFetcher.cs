namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IStreamFetcher
    {
        TrackingStreamReader Fetch(string filePath);
    }
}