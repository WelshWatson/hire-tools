using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamCityController.FakeApi
{
    public sealed class TeamCity : IFakeApi, IDisposable
    {
        public TeamCity()
        {
            // start with an artificially high build number
            _buildCounter = _rng.Next(1000, 1200);

            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    // emulate rotation of the client's CSRF token
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    _csrfToken = Guid.NewGuid().ToString();
                }
            });
        }

        public void Dispose()
        {
            _disposed = true;
        }

        public string EnqueueBuild(string csrfToken, Project project)
        {
            Thread.Sleep(ArtificialLatency);

            if (csrfToken.Equals(_csrfToken) == false) throw new Exception("403 Forbidden: CSRF Header X-TC-CSRF-Token does not match CSRF session value");

            var id = _buildCounter++;

            // start build
            _buildTasks.Add(id, new BuildTask(project, id));

            return @$"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                <build id=""{id}"" buildTypeId=""{project}"" state=""queued"" href=""/app/rest/buildQueue/id:{id}"" webUrl=""http://build.server/viewQueued.html?itemId={id}"">
                  <buildType id=""enclave_fabric_windows"" name=""Enclave Windows"" projectName=""Enclave"" projectId=""Enclave"" href=""/app/rest/buildTypes/id:{project}"" webUrl=""http://build.server/viewType.html?buildTypeId={{projectId}}""/>
                  <queuedDate>20200614T084618+0100</queuedDate>
                  <triggered type=""user"" date=""20200614T084618+0100"">
                    <user username=""services"" name=""Services Team"" id=""1"" href=""/app/rest/users/id:2""/>
                  </triggered>
                  <changes href=""/app/rest/changes?locator=build:(id:{id})""/>
                  <revisions count=""0""/>
                  <compatibleAgents href=""/app/rest/agents?locator=compatible:(build:(id:{id}))""/>
                  <artifacts href=""/app/rest/builds/id:{id}/artifacts/children/""/>
                </build>";
        }

        public string RequestBuildArtifacts(string csrfToken, int buildId)
        {
            Thread.Sleep(ArtificialLatency);

            if (csrfToken.Equals(_csrfToken) == false) throw new Exception("403 Forbidden: CSRF Header X-TC-CSRF-Token does not match CSRF session value");

            if (_buildTasks.ContainsKey(buildId))
            {
                return _buildTasks[buildId].Artifacts;
            }

            throw new Exception("Responding with error, status code: 404 (Not Found).");
        }

        public string RequestBuildStatus(string csrfToken, int buildId)
        {
            Thread.Sleep(ArtificialLatency);

            if (csrfToken.Equals(_csrfToken) == false) throw new Exception("403 Forbidden: CSRF Header X-TC-CSRF-Token does not match CSRF session value");

            if (_buildTasks.ContainsKey(buildId))
            {
                return _buildTasks[buildId].Status;
            }

            throw new Exception("Responding with error, status code: 404 (Not Found).");
        }

        public string RequestCsrfToken()
        {
            Thread.Sleep(ArtificialLatency);
            return _csrfToken;
        }

        /// <summary>
        /// random values to simulate api call network latency
        /// </summary>
        private TimeSpan ArtificialLatency => new TimeSpan(0, 0, 0, 0, _rng.Next(15, 100));

        private int _buildCounter;

        private readonly Dictionary<int, IBuildTask> _buildTasks = new Dictionary<int, IBuildTask>();

        private string _csrfToken = Guid.NewGuid().ToString();

        private bool _disposed;

        private readonly Random _rng = new Random();
    }
}