﻿using CoreService.Application.Interfaces;
using Domain.Exceptions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CoreService.Infrastructure.FileStorage
{
    public class MinioFileStorage: IFileStorage
    {
        private readonly IMinioClient _client;
        private readonly MinioConfiguration _config;

        public MinioFileStorage(IMinioClient client, IOptions<MinioConfiguration> minioConfiguration)
        {
            _client = client;
            _config = minioConfiguration.Value;
        }

        public async Task<Stream> GetFileAsync(string name, bool isTemporary = false)
        {
            Stream stream = new MemoryStream();

            var args = new GetObjectArgs()
                .WithObject((isTemporary ? $"{_config.TmpFolder}/" : "") + name)
                .WithBucket(_config.BucketName)
                .WithCallbackStream(async (str, cancellationToken) => 
                    await str.CopyToAsync(stream, cancellationToken));
            
            try
            {
                await _client.GetObjectAsync(args);
                stream.Position = 0;
            }
            catch (ObjectNotFoundException ex)
            {
                throw new NotFoundException("Object name: " + name + " " + ex);
            }
            return stream;
        }

        public async Task<string> GeneratePutUrlForTempFile(string fileName)
        {
            var args = new PresignedPutObjectArgs()
                .WithObject($"{_config.TmpFolder}/" + fileName)
                .WithBucket(_config.BucketName)
                .WithExpiry(60 * 60 * 12);

            var url = await _client.PresignedPutObjectAsync(args);
            return url.Replace(_config.Endpoint, _config.PublicUrl);
        }
    }
}
