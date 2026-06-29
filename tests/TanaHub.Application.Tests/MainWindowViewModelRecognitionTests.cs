using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Application.Updates;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.UI.Services;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task RecognizeFromFileCommand_ClearsPreviousResultWhenSelectionIsCanceled()
    {
        var files = new Queue<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>(
        [
            (new MemoryStream([1, 2, 3]), "image/png", "first.png", null),
            (null, string.Empty, "Selected image", null)
        ]);
        var viewModel = CreateViewModel(new FakeFileOpenService(files));

        await viewModel.RecognizeFromFileCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.CurrentRecognitionResult);
        Assert.NotEmpty(viewModel.RecognitionResults);

        await viewModel.RecognizeFromFileCommand.ExecuteAsync(null);

        Assert.Null(viewModel.CurrentRecognitionResult);
        Assert.Empty(viewModel.RecognitionResults);
        Assert.Empty(viewModel.RecognitionVariantResults);
        Assert.Null(viewModel.RecognitionSourcePreviewUri);
        Assert.Equal("No image selected.", viewModel.RecognitionStatus);
    }

    private static MainWindowViewModel CreateViewModel(IFileOpenService fileOpenService)
    {
        return new MainWindowViewModel(
            new FakeMediaCatalogService(),
            new FakeUserLibraryService(),
            new FakeAppSettingsService(),
            new FakeAiringScheduleService(),
            new FakeAppThemeService(),
            new FakeFileSaveService(),
            new FakeAniListAuthService(),
            new FakeAniListSyncService(),
            new FakeNotificationService(),
            new FakeCatalogSourceSelector(),
            new FakeRecognitionService(),
            new FakeRecognitionInboxService(),
            fileOpenService,
            new FakeAppUpdateService());
    }

    private sealed class FakeMediaCatalogService : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(
            MediaSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<PagedResult<MediaItem>>.Success(
                new PagedResult<MediaItem>([], query.Page, query.PageSize, 0)));
        }

        public Task<Result<MediaItem>> GetByIdAsync(
            string mediaId,
            CancellationToken cancellationToken = default)
        {
            MediaItem item = new Anime(
                mediaId,
                new MediaTitle("Cowboy Bebop", "Cowboy Bebop"),
                MediaFormat.Tv,
                MediaReleaseStatus.Finished);

            return Task.FromResult(Result<MediaItem>.Success(item));
        }
    }

    private sealed class FakeUserLibraryService : IUserLibraryService
    {
        public Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(
            UserLibraryQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<PagedResult<UserMediaEntry>>.Success(
                new PagedResult<UserMediaEntry>([], query.Page, query.PageSize, 0)));
        }

        public Task<Result<UserMediaEntry>> GetEntryAsync(
            string mediaId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<UserMediaEntry>.Failure(ApplicationError.NotFound("Not found.")));
        }

        public Task<Result<UserMediaEntry>> UpsertEntryAsync(
            UserMediaEntry entry,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<UserMediaEntry>.Success(entry));

        public Task<Result<UserMediaEntry>> IncrementProgressAsync(
            string mediaId,
            int amount = 1,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<UserMediaEntry>> UpdateStatusAsync(
            string mediaId,
            MediaListStatus status,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<UserMediaEntry>> UpdateScoreAsync(
            string mediaId,
            int? score,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<UserMediaEntry>> UpdateNotesAsync(
            string mediaId,
            string? notes,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<UserMediaEntry>> UpdateReviewAsync(
            string mediaId,
            string? review,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<UserMediaEntry>> UpdateOrganizationAsync(
            string mediaId,
            IReadOnlyList<string> tags,
            IReadOnlyList<string> customLists,
            CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);

        public Task<Result<bool>> RemoveEntryAsync(
            string mediaId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));
    }

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        public Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AppSettings>.Success(new AppSettings { RecognitionServicesEnabled = true }));

        public Task<Result<AppSettings>> SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AppSettings>.Success(settings));
    }

    private sealed class FakeAiringScheduleService : IAiringScheduleService
    {
        public Task<Result<IReadOnlyList<AiringScheduleItem>>> GetUpcomingAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<AiringScheduleItem>>.Success([]));
    }

    private sealed class FakeAppThemeService : IAppThemeService
    {
        public void Apply(string theme)
        {
        }
    }

    private sealed class FakeFileSaveService : IFileSaveService
    {
        public Task<bool> SaveTextAsync(
            string suggestedFileName,
            string content,
            string extension,
            string contentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeAniListAuthService : IAniListAuthService
    {
        public Task<Result<AniListAuthResult>> AuthorizeAsync(
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AniListAuthResult>.Failure(ApplicationError.ExternalService("Not configured.")));
    }

    private sealed class FakeAniListSyncService : IAniListSyncService
    {
        public Task<Result<int>> SyncAsync(
            string accessToken,
            int userId,
            IUserLibraryService libraryService,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(0));
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeCatalogSourceSelector : ICatalogSourceSelector
    {
        public void SetSource(string source)
        {
        }
    }

    private sealed class FakeRecognitionService : IRecognitionService
    {
        public Task<Result<IReadOnlyList<RecognitionMatch>>> RecognizeAsync(
            Stream imageStream,
            string mimeType,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RecognitionMatch> matches =
            [
                new(1, "Cowboy Bebop", "Cowboy Bebop", null, "1", 0.94, null)
            ];

            return Task.FromResult(Result<IReadOnlyList<RecognitionMatch>>.Success(matches));
        }
    }

    private sealed class FakeRecognitionInboxService : IRecognitionInboxService
    {
        public Task<Result<IReadOnlyList<RecognitionAttempt>>> GetRecentAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<RecognitionAttempt>>.Success([]));

        public Task<Result<RecognitionAttempt>> SaveAsync(
            RecognitionAttempt attempt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<RecognitionAttempt>.Success(attempt));

        public Task<Result<bool>> RemoveAsync(
            string attemptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));
    }

    private sealed class FakeFileOpenService : IFileOpenService
    {
        private readonly Queue<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> picks;

        public FakeFileOpenService(
            Queue<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> picks)
        {
            this.picks = picks;
        }

        public Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PickTextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Stream?, string, string, string?)>((null, string.Empty, "Text", null));

        public Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PickImageAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(picks.Dequeue());

        public Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PasteImageAsync(
            CancellationToken cancellationToken = default) =>
            PickImageAsync(cancellationToken);
    }

    private sealed class FakeAppUpdateService : IAppUpdateService
    {
        public Task<Result<AppUpdateCheckResult>> CheckForUpdatesAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<AppUpdateCheckResult>.Success(
                new AppUpdateCheckResult(currentVersion, null)));
        }
    }
}
