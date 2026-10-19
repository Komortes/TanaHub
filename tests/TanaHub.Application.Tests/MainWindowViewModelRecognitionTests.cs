using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Application.Updates;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.UI.Services;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
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

    [Fact]
    public async Task PasteAndRecognizeCommand_DeletesPreviousTemporaryClipboardImage()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"tanahub_paste_{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(temporaryPath, [1, 2, 3]);
        var files = new Queue<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>(
        [
            (new MemoryStream([1, 2, 3]), "image/png", "Clipboard image", temporaryPath),
            (null, string.Empty, "Clipboard image", null)
        ]);
        using var viewModel = CreateViewModel(new FakeFileOpenService(files));

        await viewModel.PasteAndRecognizeCommand.ExecuteAsync(null);
        Assert.True(File.Exists(temporaryPath));

        await viewModel.PasteAndRecognizeCommand.ExecuteAsync(null);

        Assert.False(File.Exists(temporaryPath));
    }

    private static MainWindowViewModel CreateViewModel(IFileOpenService fileOpenService, IMediaCatalogService? catalog = null, IUserLibraryService? library = null, IMediaRelationsService? relations = null)
    {
        return new MainWindowViewModel(
            catalog ?? new FakeMediaCatalogService(),
            library ?? new FakeUserLibraryService(),
            new FakeAppSettingsService(),
            new FakeAiringScheduleService(),
            new FakeAppThemeService(),
            new FakeFileSaveService(),
            new FakeAniListAuthService(),
            new FakeAniListSyncService(),
            new FakeNotificationService(),
            new FakeCatalogSourceSelector(),
            new FakeRecognitionService(),
            new FakeArtRecognitionService(),
            new FakeRecognitionInboxService(),
            fileOpenService,
            new FakeAppUpdateService(), relations);
    }

    [Fact]
    public async Task RecognizeFromFileCommand_UsesArtRecognitionWhenModeIsArt()
    {
        var files = new Queue<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>(
        [
            (new MemoryStream([1, 2, 3]), "image/png", "art.png", null)
        ]);
        var viewModel = CreateViewModel(new FakeFileOpenService(files));
        viewModel.SetRecognitionModeCommand.Execute("art");

        await viewModel.RecognizeFromFileCommand.ExecuteAsync(null);

        Assert.Single(viewModel.ArtRecognitionResults);
        Assert.Equal("Sample Art", viewModel.ArtRecognitionResults[0].Title);
        Assert.Empty(viewModel.RecognitionResults);
    }

    [Fact]
    public void SetRecognitionModeCommand_TogglesIsArtRecognitionMode()
    {
        var viewModel = CreateViewModel(new FakeFileOpenService(new()));

        Assert.False(viewModel.IsArtRecognitionMode);

        viewModel.SetRecognitionModeCommand.Execute("art");
        Assert.True(viewModel.IsArtRecognitionMode);

        viewModel.SetRecognitionModeCommand.Execute("anime");
        Assert.False(viewModel.IsArtRecognitionMode);
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
        public UserMediaEntry? Entry { get; set; }
        public TaskCompletionSource? PendingSave { get; set; }
        public int UpsertCalls { get; private set; }
        public Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(
            UserLibraryQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<PagedResult<UserMediaEntry>>.Success(
                new PagedResult<UserMediaEntry>(Entry is null ? [] : [Entry], query.Page, query.PageSize, Entry is null ? 0 : 1)));
        }

        public Task<Result<UserMediaEntry>> GetEntryAsync(
            string mediaId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entry is null
                ? Result<UserMediaEntry>.Failure(ApplicationError.NotFound("Not found."))
                : Result<UserMediaEntry>.Success(Entry));
        }

        public async Task<Result<UserMediaEntry>> UpsertEntryAsync(
            UserMediaEntry entry,
            CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            if (PendingSave is not null) await PendingSave.Task;
            Entry = entry;
            return Result<UserMediaEntry>.Success(entry);
        }

        public Task<Result<int>> MergeEntriesAsync(
            IReadOnlyCollection<UserMediaEntry> entries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(entries.Count));

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
            CancellationToken cancellationToken = default)
        {
            if (Entry is not null) Entry = Entry with { Score = score };
            return GetEntryAsync(mediaId, cancellationToken);
        }

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

        public void SetOfflineCacheEnabled(bool enabled)
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

    private sealed class FakeArtRecognitionService : IArtRecognitionService
    {
        public Task<Result<IReadOnlyList<ArtRecognitionMatch>>> RecognizeAsync(
            Stream imageStream,
            string mimeType,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ArtRecognitionMatch> matches =
            [
                new(0.9, "Sample Art", "Some Artist",
                    new Uri("https://www.pixiv.net/artworks/123"),
                    new Uri("https://img1.saucenao.com/res/thumb.jpg"))
            ];

            return Task.FromResult(Result<IReadOnlyList<ArtRecognitionMatch>>.Success(matches));
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
