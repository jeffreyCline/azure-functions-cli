﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Functions.Cli.Arm;
using Colors.Net;
using static Azure.Functions.Cli.Common.OutputTheme;
using Azure.Functions.Cli.Helpers;
using System.Net.Http;
using Azure.Functions.Cli.Common;
using Azure.Functions.Cli.Interfaces;
using Azure.Functions.Cli.Arm.Models;

namespace Azure.Functions.Cli.Actions.AzureActions
{
    [Action(Name = "list-functions", Context = Context.Azure, SubContext = Context.FunctionApp, HelpText = "List functions in a given function app on azure.")]
    internal class ListFunctionsActions : BaseFunctionAppAction
    {
        private readonly ISettings _settings;

        public ListFunctionsActions(IArmManager armManager, ISettings settings) : base(armManager)
        {
            _settings = settings;
        }

        public override async Task RunAsync()
        {
            var functionApp = await _armManager.GetFunctionAppAsync(FunctionAppName);
            if (functionApp != null)
            {
                await RetryHelper.Retry(async () =>
                {
                    using (var client = new HttpClient() { BaseAddress = new Uri($"https://{functionApp.ScmUri}") })
                    {
                        client.DefaultRequestHeaders.Authorization = await _armManager.GetAuthenticationHeader(_settings.CurrentSubscription);

                        var response = await client.GetAsync(new Uri("api/functions", UriKind.Relative));

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new CliException($"Error trying to retrieve list of functions ({response.StatusCode}).");
                        }

                        var functions = await response.Content.ReadAsAsync<IEnumerable<FunctionInfo>>();

                        ColoredConsole.WriteLine(TitleColor($"Functions in {FunctionAppName}:"));
                        foreach (var function in functions)
                        {
                            var trigger = function
                                .Config?["bindings"]
                                ?.FirstOrDefault(o => o["type"]?.ToString().IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) != -1)
                                ?["type"];

                            trigger = trigger ?? "No Trigger Found";

                            ColoredConsole.WriteLine($"    {function.Name} - [{VerboseColor(trigger.ToString())}]");
                        }
                    }
                }, 2);
            }
            else
            {
                ColoredConsole.Error.WriteLine(ErrorColor($"Can't find function app by name {FunctionAppName}"));
            }
        }
    }
}
