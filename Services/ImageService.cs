using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace GadgetVault.Services
{
    public class ImageService
    {
        private readonly Cloudinary _cloudinary;

        public ImageService()
        {
            var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
            var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string?> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "GadgetVault/Branding",
                Transformation = new Transformation().Width(800).Height(400).Crop("limit")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary Upload Error: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }

        public async Task<string?> UploadProfilePictureAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "GadgetVault/Profiles",
                Transformation = new Transformation().Width(250).Height(250).Crop("fill").Gravity("face")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl?.ToString();
        }

        public async Task DeleteImageAsync(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                // Extract PublicID from URL
                // Example: https://res.cloudinary.com/demo/image/upload/v12345678/GadgetVault/Profiles/xyz.png
                var parts = imageUrl.Split('/');
                var filenameWithExtension = parts[^1];
                var filename = filenameWithExtension.Split('.')[0];
                
                // We need the full path in the folder
                var publicId = $"GadgetVault/Profiles/{filename}";
                
                await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            }
            catch { /* Best effort deletion */ }
        }
    }
}
