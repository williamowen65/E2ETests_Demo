using E2ETests_Demo.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace CloudTests
{
    [TestClass]
    public class PlaywrightTests
    {
        private WebApplicationFactory<E2ETests_Demo.Program> _factory;
        private SqliteTestFixture _sqliteFixture;
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IPage _page;
        private string _baseUrl;

        [TestInitialize]
        public async Task Setup()
        {
            Console.WriteLine("Setting up Playwright test with SQLite");

            // Create SQLite test fixture
            _sqliteFixture = new SqliteTestFixture();

            // Setup test server with SQLite database and explicitly configure the host
            _factory = new WebApplicationFactory<E2ETests_Demo.Program>()
                .WithWebHostBuilder(builder =>
                {
                    // Explicitly set server URLs to use a fixed port for testing
                    builder.UseUrls("https://localhost:5501");

                    // This environment toggles DB connection for the project build
                    builder.UseEnvironment("Testing");

                    builder.ConfigureServices(services =>
                    {
                        // Remove existing DbContext registration
                        var descriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                        if (descriptor != null)
                        {
                            services.Remove(descriptor);
                        }

                        // Get the SQLite DbContext options from the fixture's service provider
                        var sqliteServiceScope = _sqliteFixture.ServiceProvider.CreateScope();
                        var sqliteOptions = sqliteServiceScope.ServiceProvider
                            .GetRequiredService<DbContextOptions<ApplicationDbContext>>();

                        // Add the SQLite DbContext options to the test server
                        services.AddDbContext<ApplicationDbContext>(options =>
                        {
                            // Copy options from the SQLite fixture
                            options.UseSqlite(
                                ((DbContextOptions<ApplicationDbContext>)sqliteOptions)
                                .FindExtension<Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal.SqliteOptionsExtension>()
                                .Connection);
                        });

                        // Replace the HttpClient with a mock version for testing
                        services.AddTransient<HttpClient>(sp =>
                        {
                            // Create a mock HttpClient that returns fake responses for local requests
                            var mockHandler = new MockHttpMessageHandler();
                            return new HttpClient(mockHandler)
                            {
                                BaseAddress = new Uri("http://localhost:5000")
                            };
                        });
                    });
                });

            // Create client with specific options
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost:5501")
            });

            _baseUrl = client.BaseAddress.ToString().TrimEnd('/');
            Console.WriteLine($"Test server URL: {_baseUrl}");

            // Setup Playwright browser
            _playwright = await Playwright.CreateAsync();
            _browser = await PlaywrightFixture.InitializeBrowserAsync(headless: false);
            _page = await _browser.NewPageAsync();

            // Add console logging from browser
            _page.Console += (_, msg) => Console.WriteLine($"Browser console: {msg.Text}");

            // Add network logging
            _page.Request += (_, req) => Console.WriteLine($"Browser request: {req.Method} {req.Url}");
            _page.Response += (_, res) => Console.WriteLine($"Browser response: {res.Status} {res.Url}");

            // Wait briefly to ensure server is fully ready
            await Task.Delay(2000);
        }

        [TestMethod]
        public async Task Test_Server_Should_Be_Running()
        {
            await _page.GotoAsync(_baseUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Console.WriteLine("Navigated to home page and should see the logo");
            string? pageTextContent = await _page.TextContentAsync("header");
            Assert.IsTrue(pageTextContent is not null && pageTextContent.Contains("Atlas"), "Test App is loaded properly");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _browser?.CloseAsync();
            _playwright?.Dispose();
            _factory?.Dispose();
            _sqliteFixture?.Dispose();
        }
    }

    // Mock HTTP handler that returns successful responses for local requests
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Check if the request is to localhost
            if (request.RequestUri.Host.Contains("localhost"))
            {
                Console.WriteLine($"Mock HTTP handler intercepted request to: {request.RequestUri}");

                // Return a mock successful response
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{ \"success\": true }", Encoding.UTF8, "application/json")
                });
            }

            // For all other requests, create a real HTTP client and forward the request
            var httpClient = new HttpClient();
            return httpClient.SendAsync(request, cancellationToken);
        }
    }
}