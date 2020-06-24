using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using TeamCityController.FakeApi;

namespace TeamCityController
{
    public class Program
    {
        private static void Main()
        {
            var api = new TeamCity();

            // request a csrf token and enqueue a build of the EnclaveWindows project
            var csrf = api.RequestCsrfToken();
            var buildXml = api.EnqueueBuild(csrf, Project.EnclaveWindows);

            // extract the value of the id attribute from the build tag <build id="1007" buildTypeId="EnclaveWindows" state="queued" ... /> 
            var id = Convert.ToInt32(XDocument.Parse(buildXml).Elements()
                .SingleOrDefault(n => n.Name == "build").Attributes()
                .SingleOrDefault(n => n.Name == "id").Value);

            while (true)
            {
                // ask the mock api for an update on the progress of the build
                var buildStatusXml = api.RequestBuildStatus(csrf, id);

                // extract the value of the percentageComplete attribute from the running-info tag: <running-info percentageComplete="17" elapsedSeconds="9" ... />
                var percentComplete = XDocument.Parse(buildStatusXml).Root.Elements()
                    .SingleOrDefault(n => n.Name == "running-info").Attributes()
                    .SingleOrDefault(n => n.Name == "percentageComplete").Value;

                Console.CursorTop = 0;
                Console.CursorVisible = false;
                Console.WriteLine($"Building {percentComplete}%");
                Console.ReadLine();
            }
        }

        public static byte[] Download(string url)
        {
            var request = WebRequest.Create(url);
            var response = request.GetResponse();
            var stream = (response as HttpWebResponse)?.GetResponseStream() ?? throw new InvalidOperationException();

            var buffer = new byte[256];
            using (var memoryStream = new MemoryStream())
            {
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return memoryStream.ToArray();
            }
        }
    }
}
