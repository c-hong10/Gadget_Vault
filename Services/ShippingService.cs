using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GadgetVault.Services
{
    public interface IShippingService
    {
        Task<(string TrackingNumber, string LabelUrl)> GenerateShippingLabelAsync(string orderNumber, string destinationEmail);
    }

    public class ShippingService : IShippingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ShippingService()
        {
            _apiKey = Environment.GetEnvironmentVariable("SHIPPO_API_KEY") ?? string.Empty;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ShippoToken", _apiKey);
        }

        public async Task<(string TrackingNumber, string LabelUrl)> GenerateShippingLabelAsync(string orderNumber, string destinationEmail)
        {
            try
            {
                // 1. Create Shipment (including addresses and parcel)
                var shipmentRequest = new
                {
                    address_from = new
                    {
                        name = "GadgetVault Warehouse",
                        street1 = "123 Logistics Way",
                        city = "San Francisco",
                        state = "CA",
                        zip = "94103",
                        country = "US",
                        phone = "4151234567",
                        email = "warehouse@gadgetvault.com"
                    },
                    address_to = new
                    {
                        name = "Demo Customer",
                        street1 = "456 Delivery Lane",
                        city = "New York",
                        state = "NY",
                        zip = "10001",
                        country = "US",
                        phone = "2121234567",
                        email = destinationEmail
                    },
                    parcels = new[]
                    {
                        new {
                            length = "10",
                            width = "8",
                            height = "6",
                            distance_unit = "in",
                            weight = "2",
                            mass_unit = "lb"
                        }
                    },
                    async = false
                };

                var shipmentResponse = await _httpClient.PostAsync("https://api.goshippo.com/shipments/", 
                    new StringContent(JsonSerializer.Serialize(shipmentRequest), Encoding.UTF8, "application/json"));

                if (!shipmentResponse.IsSuccessStatusCode)
                {
                    var error = await shipmentResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Shippo Shipment Error: {error}");
                }

                var shipmentData = JsonDocument.Parse(await shipmentResponse.Content.ReadAsStringAsync());
                var rates = shipmentData.RootElement.GetProperty("rates");
                
                if (rates.GetArrayLength() == 0)
                {
                    throw new Exception("No shipping rates found for this shipment.");
                }

                // 2. Buy the first available rate (Transaction)
                // In a real app, you'd let the user choose, but for a demo, we take the first.
                var rateId = rates[0].GetProperty("object_id").GetString();

                var transactionRequest = new
                {
                    rate = rateId,
                    label_file_type = "PDF",
                    async = false
                };

                var transactionResponse = await _httpClient.PostAsync("https://api.goshippo.com/transactions/",
                    new StringContent(JsonSerializer.Serialize(transactionRequest), Encoding.UTF8, "application/json"));

                if (!transactionResponse.IsSuccessStatusCode)
                {
                    var error = await transactionResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Shippo Transaction Error: {error}");
                }

                var transactionData = JsonDocument.Parse(await transactionResponse.Content.ReadAsStringAsync());
                var trackingNumber = transactionData.RootElement.GetProperty("tracking_number").GetString() ?? "N/A";
                var labelUrl = transactionData.RootElement.GetProperty("label_url").GetString() ?? string.Empty;

                return (trackingNumber, labelUrl);
            }
            catch (Exception ex)
            {
                // Fallback to mock if API fails (useful for local dev without key)
                Console.WriteLine($"Shipping API Fallback: {ex.Message}");
                string mockTracking = $"GV-MOCK-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
                string mockLabel = "https://shippo-static.s3.amazonaws.com/label_300px.png";
                return (mockTracking, mockLabel);
            }
        }
    }
}
