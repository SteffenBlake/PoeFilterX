namespace PoeFilterX.Business.Services
{
    public interface IStreamFetcher
    {
        TrackingStreamReader Fetch(string filePath);
    }
}