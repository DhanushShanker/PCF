
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace PostReplyCreationPlugin
    {
        public class PostPlugin : IPlugin
        {
            public void Execute(IServiceProvider serviceProvider)
            {
                ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(null);

                tracingService.Trace("PostPlugin execution started.");

                // Get the context from the service provider
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                // Ensure that the entity is of type ats_post or ats_reply
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity)
                {
                    if (entity.LogicalName != "ats_post" && entity.LogicalName != "ats_reply")
                    {
                        tracingService.Trace("Entity is neither ats_post nor ats_reply. Exiting plugin.");
                        return;
                    }

                    tracingService.Trace($"Processing {entity.LogicalName} entity.");

                    // Get the content of the post or reply
                    string content = entity.GetAttributeValue<string>("ats_content");
                    tracingService.Trace($"Content: {content}");

                    // Extract tagged usernames from the content
                    var taggedUsernames = ExtractTaggedUsernames(content);
                    tracingService.Trace($"Tagged usernames: {string.Join(", ", taggedUsernames)}");

                    foreach (var username in taggedUsernames)
                    {
                        // Fetch the email address of the user
                        tracingService.Trace($"Fetching email for user: {username}");
                        var email = FetchUserEmail(service, username, tracingService);
                        if (email != null)
                        {
                            tracingService.Trace($"Sending email to: {email}");
                            // Send email using the MailHelper
                            MailHelper.Send(service, email, "You were tagged in a post/reply", $"You were tagged in a post/reply: {content}");
                        }
                        else
                        {
                            tracingService.Trace($"No email found for user: {username}");
                        }
                    }
                }

                tracingService.Trace("PostPlugin execution finished.");
            }

            private string[] ExtractTaggedUsernames(string content)
            {
                // Adjusted regex to allow spaces and other characters in usernames
                var regex = new Regex(@"@([^\s@]+(?:\s[^\s@]+)*)");
                var matches = regex.Matches(content);
                return matches.Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            }

            private string FetchUserEmail(IOrganizationService service, string username, ITracingService tracingService)
            {
                var query = new QueryExpression("systemuser")
                {
                    ColumnSet = new ColumnSet("internalemailaddress"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                    {
                        new ConditionExpression("fullname", ConditionOperator.Equal, username)
                    }
                    }
                };

                var result = service.RetrieveMultiple(query);
                var user = result.Entities.FirstOrDefault();
                var email = user?.GetAttributeValue<string>("internalemailaddress");

                if (email == null)
                {
                    tracingService.Trace($"No email found for username: {username}");
                }
                else
                {
                    tracingService.Trace($"Email for {username} is {email}");
                }

                return email;
            }
        }
    }

          
