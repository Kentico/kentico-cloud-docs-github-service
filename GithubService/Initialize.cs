using System;
using System.Collections.Generic;
using System.Net.Http;
using GithubService.Repository;
using GithubService.Services;
using GithubService.Services.Clients;
using GithubService.Services.Parsers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using GithubService.Models;

namespace GithubService
{
    public static class Initialize
    {
        [FunctionName("kcd-github-service-initialize")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            HttpRequest request,
            ILogger logger)
        {
            logger.LogInformation("Initialize called.");

            try
            {
                var configuration = new Configuration.Configuration();
                var fileParser = new FileParser();

                // Get all the files from GitHub
                var githubClient = new GithubClient(
                    new HttpClient(),
                    configuration.GithubRepositoryName,
                    configuration.GithubRepositoryOwner,
                    configuration.GithubAccessToken);
                var githubService = new Services.GithubService(githubClient, fileParser);
                var codeFiles = await githubService.GetCodeFilesAsync();

                // Persist all code sample files
                var connectionString = configuration.RepositoryConnectionString;
                var codeFileRepository = await CodeFileRepositoryProvider.CreateCodeFileRepositoryInstance(connectionString);
                var fragmentsToUpsert = new List<CodeFragment>();

                foreach (var codeFile in codeFiles)
                {
                    await codeFileRepository.StoreAsync(codeFile);
                    fragmentsToUpsert.AddRange(codeFile.CodeFragments);
                }

                // Store code fragment event
                var eventDataRepository = await EventDataRepository.CreateInstance(connectionString);
                await new EventDataService(eventDataRepository)
                    .SaveCodeFragmentEventAsync(FunctionMode.Initialize, fragmentsToUpsert);

                return new OkObjectResult("Initialized.");
            }
            catch (Exception exception)
            {
                // This try-catch is required for correct logging of exceptions in Azure
                var message = $"Exception: {exception.Message}\nStack: {exception.StackTrace}";

                throw new GithubServiceException(message);
            }
        }
    }
}