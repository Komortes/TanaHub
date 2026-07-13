namespace TanaHub.Application.Services;

public interface ICatalogSourceSelector
{
    void SetSource(string source);

    void SetOfflineCacheEnabled(bool enabled);
}
