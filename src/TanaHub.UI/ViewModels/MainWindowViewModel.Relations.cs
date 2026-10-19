using TanaHub.Application.Queries;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? relatedTitlesCancellation;
    private readonly List<string> relatedTitleHistory = [];

    public Task RelatedTitlesLoad { get; private set; } = Task.CompletedTask;

    private Task RetryRelatedTitlesAsync(string mediaId) => SelectedMedia?.Id == mediaId
        ? StartRelatedTitlesLoadAsync(mediaId, detailLoadVersion, forceRefresh: true)
        : Task.CompletedTask;

    private async Task OpenRelatedTitleAsync(string mediaId)
    {
        var previousTitle = SelectedMedia?.Id;
        if (previousTitle == mediaId) return;
        if (await LoadDetailAsync(mediaId) && previousTitle is not null)
        {
            if (relatedTitleHistory.Count == 50) relatedTitleHistory.RemoveAt(0);
            relatedTitleHistory.Add(previousTitle);
        }
    }

    private Task StartRelatedTitlesLoadAsync(string mediaId, int version, bool forceRefresh = false)
    {
        if (version != detailLoadVersion || !IsDetailVisible ||
            SelectedMedia?.RelatedTitles is not { } section || mediaRelationsService is null)
            return Task.CompletedTask;

        CancelRelatedTitlesLoad();
        var cancellation = new CancellationTokenSource();
        relatedTitlesCancellation = cancellation;
        section.BeginLoading();
        RelatedTitlesLoad = LoadRelatedTitlesAsync(mediaId, section, version, forceRefresh, cancellation.Token);
        return RelatedTitlesLoad;
    }

    private async Task LoadRelatedTitlesAsync(
        string mediaId, RelatedTitlesViewModel section, int version, bool forceRefresh, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediaRelationsService!.GetRelatedTitlesAsync(mediaId, forceRefresh, cancellationToken);
            if (!IsCurrent()) return;
            if (result.IsFailure)
            {
                section.ShowError(result.Error.Message);
                return;
            }

            var library = await userLibraryService.GetEntriesAsync(new UserLibraryQuery { PageSize = int.MaxValue }, cancellationToken);
            if (!IsCurrent()) return;
            IReadOnlyList<UserMediaEntry> entries = library.IsSuccess ? library.Value!.Items : [];
            section.Apply(result.Value!, entries, OpenRelatedTitleAsync);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            if (IsCurrent()) section.ShowError("Related titles are unavailable right now. Try again.");
        }

        bool IsCurrent() => !cancellationToken.IsCancellationRequested && version == detailLoadVersion &&
            IsDetailVisible && ReferenceEquals(SelectedMedia?.RelatedTitles, section);
    }

    private void CancelRelatedTitlesLoad()
    {
        relatedTitlesCancellation?.Cancel();
        relatedTitlesCancellation?.Dispose();
        relatedTitlesCancellation = null;
    }
}
