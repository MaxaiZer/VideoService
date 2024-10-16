using CoreService.Application.Dto;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CoreService.IntegrationTests.Tools;
using Domain.Entities;
using FluentAssertions;

namespace CoreService.IntegrationTests.Tests
{
    [Collection("Environment collection")]
    public class VideoControllerTests : IClassFixture<TestingWebAppFactory>
    {
        private readonly HttpClient _client;
        private const string _baseUrl = "api/videos";

        public VideoControllerTests(TestingWebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetUploadUrl_WhenUnauthorizedUser_ShouldReturnUnauthorized()
        {
            var response = await _client.GetAsync($"{_baseUrl}/upload-url");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task GetVideoMetadata_WhenVideoDoesntExist_ShouldReturn404()
        {
            var response = await _client.GetAsync($"{_baseUrl}/12345");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetVideoMetadata_WhenVideoNotProcessed_ShouldReturn404()
        {
            var response = await _client.GetAsync($"{_baseUrl}/{DatabaseSeeder.notProcessedVideo.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetVideoMetadata_WhenVideoProcessed_ShouldBeSuccessful()
        {
            var response = await _client.GetAsync($"{_baseUrl}/{DatabaseSeeder.processedVideo.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var viewableVideo = await response.Content.ReadFromJsonAsync<ViewableVideoMetadata>();
            viewableVideo.Should().NotBeNull();
        }
        
        [Fact]
        public async Task GetVideosMetadata_WhenVideoProcessed_ShouldBeSuccessful()
        {
            var response = await _client.GetAsync($"{_baseUrl}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var viewableVideos = await response.Content.ReadFromJsonAsync<List<ViewableVideoMetadata>>();
            viewableVideos.Should().NotBeNull();
            viewableVideos.Should().NotBeEmpty();
        }
        
        [Fact]
        public async Task UploadVideo_WhenUnauthorizedUser_ShouldReturnUnauthorized()
        {
            var response = await UploadVideoInfo(
                new VideoUploadDto
                {
                    Name = "rabbit",
                    Description = "rabbit",
                    UploadedVideoId = "filename"
                });
            
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task UploadVideo_WhenValidUserUploadsVideo_ShouldBeSuccessful()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DatabaseSeeder.existingUsersWithActiveTokens[0].AccessToken);
            var tempFilePath = Path.GetTempFileName();
            
            try
            {
                await UploadVideoAndInfo(tempFilePath);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
       
       /* [Fact]
        public async Task GetMasterPlaylist_WhenVideoUploaded_ShouldReturnValidPlaylist()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DatabaseSeeder.existingUserWithActiveToken.AccessToken);
            string videoId = await UploadVideoAndData("TestData/rabbit320.mp4");
            
            var getPlaylistResponse = await _client.GetAsync($"{_baseUrl}/{videoId}/file/master-playlist");
            getPlaylistResponse.EnsureSuccessStatusCode();
            
            var playlistContent = await getPlaylistResponse.Content.ReadAsStringAsync();
            await TryParseMasterPlaylist(playlistContent, $"{_baseUrl}/{videoId}");
        }
*/
    /*    async Task TryParseMasterPlaylist(string masterPlaylistContent, string baseUrl)
        {
            var playlistUrl = HlsParser.ExtractFirstPlaylistUrl(masterPlaylistContent);
            var playlistFullUrl = baseUrl + "/" + playlistUrl;
            var getPlaylistResponse = await _client.GetAsync(playlistFullUrl);
            if (!getPlaylistResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Failed to fetch playlist by url: " + baseUrl + "/" + playlistUrl + 
                                               " with status code " + getPlaylistResponse.StatusCode);
            }
            
            var playlistContent = await getPlaylistResponse.Content.ReadAsStringAsync();
            
            var segmentUrl = HlsParser.ExtractFirstSegmentUrl(playlistContent);
            var getSegmentResponse = await _client.GetAsync(playlistFullUrl.RemoveLastSegmentInUrl() + segmentUrl);
            if (!getSegmentResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Failed to fetch segment by url: " + baseUrl + "/" + segmentUrl + 
                                               " with status code " + getSegmentResponse.StatusCode);
            }
        }
        */
        async Task<string> UploadVideoAndInfo(string videoFilePath)
        {
            var getUrlResponse = await _client.GetAsync($"{_baseUrl}/upload-url");
            getUrlResponse.EnsureSuccessStatusCode();
            
            var getUrlResponseContent = await getUrlResponse.Content.ReadAsStringAsync();
            var uploadUrlResult = JsonConvert.DeserializeObject<GeneratedUploadUrlDto>(getUrlResponseContent);

            var uploadFileResponse = await UploadVideoFile(videoFilePath, uploadUrlResult.Url, useOwnHttpClient: true);
            
            try
            {
                uploadFileResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"The request to {uploadUrlResult.Url} failed with " +
                                               $"content {uploadFileResponse.Content.ReadAsStringAsync()} " +
                                               $"and message: {ex.Message}", ex);
            }
            
            var uploadDataResponse = await UploadVideoInfo(
                new VideoUploadDto
                {
                    Name = "rabbit",
                    Description = "rabbit",
                    UploadedVideoId = uploadUrlResult.FileName
                });
            uploadDataResponse.EnsureSuccessStatusCode();

            return uploadUrlResult.FileName;
        }
        
        async Task<HttpResponseMessage> UploadVideoFile(string filePath, string putUrl, bool useOwnHttpClient)
        {
            using var videoStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var videoContent = new StreamContent(videoStream);
            videoContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

            if (!useOwnHttpClient) 
                return await _client.PutAsync(putUrl, videoContent);
            
            using var client = new HttpClient();
            return await client.PutAsync(putUrl, videoContent);
        }
        
        async Task<HttpResponseMessage> UploadVideoInfo(VideoUploadDto data)
        {
            var uploadInfoJson = JsonConvert.SerializeObject(data);
            var uploadInfoContent = new StringContent(uploadInfoJson, Encoding.UTF8, "application/json");

            return await _client.PostAsync(_baseUrl, uploadInfoContent);
        }
    }
}