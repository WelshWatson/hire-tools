using System;
using System.Text;

namespace TeamCityController.FakeApi
{
    public sealed class BuildTask : IBuildTask
    {
        public BuildTask(Project project, int buildId)
        {
            Project = project;
            Id = buildId;

            // set the duration of this build to take between 20 to 75 seconds
            _buildDuration = new TimeSpan(0, 0, 0, _rng.Next(20, 75));
            _buildEnd = DateTime.Now.Add(_buildDuration);
            _buildStart = DateTime.Now;
        }

        public string Artifacts
        {
            get
            {
                if (DateTime.Now > _buildEnd)
                {
                    var count = _rng.Next(1, 4);
                    var filesXml = new StringBuilder();

                    for (var i = 0; i < count; i++)
                    {
                        var filename = _artifactFileNames[_rng.Next(0, _artifactFileNames.Length)];
                        var href = _artifactPool[_rng.Next(0, _artifactPool.Length)];

                        filesXml.Append(@$"<file name=""{filename}"">
                                <content href=""{href}""/>
                            </file>");
                    }

                    return @$"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                    <files count=""{count}"">
                      {filesXml}
                    </files>";
                }

                return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                    <files count=""0""/>";
            }
        }

        public string Status
        {
            get
            {
                if (DateTime.Now > _buildEnd)
                {
                    // build completed
                    return @$"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                        <build id=""{Id}"" buildTypeId=""{Project}"" status=""SUCCESS"" state=""finished"" href=""/app/rest/builds/id:{Id}"" webUrl=""http://build.server/viewLog.html?buildId={Id}&amp;buildTypeId={Project}"">
                          <statusText>Success</statusText>
                          <buildType id=""{Project}"" name=""Enclave"" projectId=""Enclave"" href=""/app/rest/buildTypes/id:{Project}"" webUrl=""http://build.server/viewType.html?buildTypeId={Project}""/>
                          <queuedDate>{_buildStart:s}</queuedDate>
                          <startDate>{_buildStart:s}</startDate>
                          <finishDate>{_buildEnd:s}</finishDate>
                          <agent id=""1"" name=""WIN-O1NTQLKI7CU"" typeId=""1"" href=""/app/rest/agents/id:1"" webUrl=""http://build.server/agentDetails.html?id=1&amp;agentTypeId=1&amp;realAgentName=WIN-O1NTQLKI7CU""/>
                          <artifacts href=""/app/rest/builds/id:{Id}/artifacts/children/""/>
                        </build>";
                }

                // build in progress
                var elapsed = DateTime.Now.Subtract(_buildStart);
                var percentComplete = elapsed == TimeSpan.Zero ? 0 : Math.Round(elapsed.TotalSeconds / _buildDuration.TotalSeconds * 100);
                var estimatedTotalSeconds = _buildDuration.TotalSeconds;

                return @$"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                    <build id=""{Id}"" buildTypeId=""{Project}"" status=""SUCCESS"" state=""running"" percentageComplete=""20"" href=""/app/rest/builds/id:{Id}"" webUrl=""http://build.server/viewLog.html?buildId={Id}&amp;buildTypeId={Project}"">
                      <statusText>dotnet publish</statusText>
                      <buildType id=""{Project}"" name=""Enclave"" projectId=""Enclave"" href=""/app/rest/buildTypes/id:{Project}"" webUrl=""http://build.server/viewType.html?buildTypeId={Project}""/>
                      <running-info percentageComplete=""{percentComplete}"" elapsedSeconds=""{elapsed.Seconds}"" estimatedTotalSeconds=""{estimatedTotalSeconds}"" outdated=""false"" probablyHanging=""false""/>
                      <queuedDate>{_buildStart:s}</queuedDate>
                      <startDate>{_buildStart:s}</startDate>
                      <agent id=""1"" name=""WIN-O1NTQLKI7CU"" typeId=""1"" href=""/app/rest/agents/id:1"" webUrl=""http://build.server/agentDetails.html?id=1&amp;agentTypeId=1&amp;realAgentName=WIN-O1NTQLKI7CU""/>
                      <artifacts count=""0"" href=""/app/rest/builds/id:{Id}/artifacts/children/""/>
                    </build>";
            }
        }

        private readonly string[] _artifactPool = {
            "http://mirror.nl.leaseweb.net/speedtest/10mb.bin", // amsterdam
            "http://mirror.de.leaseweb.net/speedtest/10mb.bin", // germany
            "http://mirror.sg.leaseweb.net/speedtest/10mb.bin", // singapore
            "http://mirror.wdc1.us.leaseweb.net/speedtest/10mb.bin", // washington
            "http://mirror.dal10.us.leaseweb.net/speedtest/10mb.bin", // dallas
            "http://mirror.sfo12.us.leaseweb.net/speedtest/10mb.bin" // san francisco
        };

        private readonly string[] _artifactFileNames = {
            "enclave_setup-2020.6.14.0.exe",
            "enclave_linux-arm64-2020.6.14.0.tar.gz",
            "enclave_linux-x64-2020.6.14.0.tar.gz",
            "enclave-windows-x64-2020.6.14.0.zip"
        };

        private readonly TimeSpan _buildDuration;

        private readonly DateTime _buildEnd;

        private readonly DateTime _buildStart;
        
        private int Id { get; }

        private Project Project { get; }

        private readonly Random _rng = new Random();
    }
}