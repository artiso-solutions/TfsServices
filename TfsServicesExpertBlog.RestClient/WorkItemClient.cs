using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DataContracts.WorkItemLinkDataTypes;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;
using TfsServicesExpertBlog.DataContracts;
using TfsServicesExpertBlog.DataContracts.FullyExpandedWorkItem;

namespace TfsServicesExpertBlog.RestClient
{
    /// <summary>
    /// This class is communicating with the VSO RestApi and sends the patch operations for the WorkItems.
    /// </summary>
    public class WorkItemClient
    {
        private readonly string account;
        private readonly string collection;
        private readonly string user;
        private readonly string password;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkItemClient"/> class.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="collection">The collection.</param>
        /// <param name="user">The user.</param>
        /// <param name="password">The password.</param>
        public WorkItemClient(string account, string collection, string user, string password)
        {
            this.account = account;
            this.collection = collection;
            this.user = user;
            this.password = password;
        }

        /// <summary>
        /// Creates the new linked work item for the Product Backlog Item. 
        /// </summary>
        /// <param name="teamProject">The team project.</param>
        /// <param name="newWorkItemTitle">The new work item title.</param>
        /// <param name="workItemType">Type of the work item.</param>
        /// <param name="workItemId">The paren work item identifier.</param>
        /// <param name="iteration">The iteration.</param>
        /// <param name="description">The description.</param>
        /// <returns>A task to await, the result is the newly created WorkItem</returns>
        public async Task<WorkItem> CreateNewLinkedWorkItem(string teamProject, string newWorkItemTitle, string workItemType, int workItemId, string iteration, string description)
        {
            WorkItem createdWorkItem;
            var json = this.CreateJsonPatchDocument(newWorkItemTitle, workItemId, iteration, description);
            var jsonString = JsonConvert.SerializeObject(json);

            var uri = $"https://{this.account}.visualstudio.com/{this.collection}/{teamProject}/_apis/wit/workitems/${workItemType}?api-version=1.0";
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json-patch+json");

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json-patch+json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{this.user}:{this.password}")));

                using (var response = client.PatchAsync(uri, content).Result)
                {
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    createdWorkItem = JsonConvert.DeserializeObject<WorkItem>(responseBody);
                }
            }
            return createdWorkItem;
        }

        public async Task<FullyExpandedWorkItem> ReadFullyExpandedWorkItem(int workItemId)
        {
            FullyExpandedWorkItem workItem;
            var uri = $"https://{this.account}.visualstudio.com/{this.collection}/_apis/wit/workitems/{workItemId}?$expand=all&api-version=1.0";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{this.user}:{this.password}")));

                using (HttpResponseMessage response = client.GetAsync(uri).Result)
                {
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    workItem = JsonConvert.DeserializeObject<FullyExpandedWorkItem>(responseBody);
                }
                return workItem;
            }
        }

        private JsonPatchDocument CreateJsonPatchDocument(string newWorkItemTitle, int workItemId, string iteration, string description)
        {
            var json = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = newWorkItemTitle
                }
            };

            if (iteration != null)
            {
                json.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.IterationPath",
                    Value = iteration
                });
            }
            if (description != null)
            {
                json.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Description",
                    Value = description
                });
            }
            if (workItemId > 0)
            {
                json.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new WorkItemLink
                    {
                        Rel = "System.LinkTypes.Hierarchy-Reverse",
                        Url = $"https://{this.account}.visualstudio.com/{this.collection}/_apis/wit/workItems/{workItemId}",
                        Attributes = new Attributes()
                        {
                            Comment = "Created by TFSServicesForExpertBlog"
                        }
                    }
                });
            }
            return json;
        }
    }
}
