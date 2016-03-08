using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using DataContracts.WorkItemChangedEventJsonTypes;
using Example;
using Newtonsoft.Json;
using TfsServicesExpertBlog.DataContracts.FullyExpandedWorkItem;
using TfsServicesExpertBlog.RestClient;

namespace Test.Controllers
{
    public class WorkItemChangedController : ApiController
    {
        private readonly WorkItemClient workItemClient = new WorkItemClient("YOUR MS ACCOUNT NAME", "YOUR VSO COLLECTION", "", "YOUR SECURITY TOKEN FOR VSO OR TFS");
        private const string Username = "YOUR DEFINED USERNAME";
        private const string Password = "YOUR DEFINED PASSWORD";

        [HttpPost]
        public async Task<HttpResponseMessage> WorkItemChanged()
        {

            if (this.Request.Headers.Authorization.Scheme == "Basic" &&
                this.Request.Headers.Authorization.Parameter ==
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}")))
            {
                var content = this.Request.Content.ReadAsStringAsync().Result;

                try
                {
                    var deserializedWorkItem = JsonConvert.DeserializeObject<WorkItemChangedEvent>(content);

                    if (deserializedWorkItem.Resource?.Fields?.SystemBoardColumn == null)
                    {
                        return this.Request.CreateResponse(HttpStatusCode.Accepted, content);
                    }

                    var systemBoardColumn = deserializedWorkItem.Resource.Fields.SystemBoardColumn;

                    if (IsInBoardColumn(systemBoardColumn, "Committed"))
                    {
                        var parentWorkItemId = deserializedWorkItem.Resource.WorkItemId;

                        // Here you fetch the complete Information of the ChangedWorkItem 
                        var workItem = await this.workItemClient.ReadFullyExpandedWorkItem(parentWorkItemId);
                        var titleToSearch = "Make it Done";

                        var tasksWithTitle = new List<FullyExpandedWorkItem>();

                        // Here is checked if the Task already exists
                        await this.GetTaskByTitle(workItem.Relations, titleToSearch, tasksWithTitle);

                        if (tasksWithTitle.Count == 0)
                        {
                            await this.workItemClient.CreateNewLinkedWorkItem(workItem.Fields.SystemTeamProject,"Make it Done", "Task", parentWorkItemId, workItem.Fields.SystemIterationPath, "Do it!");
                        }
                    }

                    content = JsonConvert.SerializeObject(deserializedWorkItem);
                }
                catch (Exception exception)
                {
                    //// HERE WOULD BE A GOOD PLACE TO SEND THE EXCEPTION TO APPLICATION INSIGHTS
                }

                if (content.Contains("Approved"))
                {
                    return this.Request.CreateResponse(HttpStatusCode.OK, content);
                }

                return this.Request.CreateResponse(HttpStatusCode.Accepted, content);
            }
            return this.Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Not allowed");
        }

        private static bool IsInBoardColumn(SystemBoardColumn systemBoardColumn, string boardColumnTitle)
        {
            return systemBoardColumn.NewValue.Contains(boardColumnTitle) &&
                   !systemBoardColumn.OldValue.Contains(boardColumnTitle);
        }

        private async Task GetTaskByTitle(IEnumerable<FullyExpandedWorkItem.Relation> relations, string titleToSearch, ICollection<FullyExpandedWorkItem> tasksWithTitle)
        {
            if (relations == null)
            {
                return;
            }

            foreach (var relatedWorkItem in relations)
            {
                var matchDigitsRegex = new Regex(@"[\d]+$");
                var match = matchDigitsRegex.Match(relatedWorkItem.Url).ToString();
                var relatedWorkItemId = Convert.ToInt32(match);
                var taskToCheck = await this.workItemClient.ReadFullyExpandedWorkItem(relatedWorkItemId);
                if (taskToCheck.Fields.SystemTitle == titleToSearch)
                {
                    tasksWithTitle.Add(taskToCheck);
                }
            }
        }
    }
}
