namespace TeamCityController.FakeApi
{
    public interface IFakeApi
    {
        string EnqueueBuild(string csrfToken, Project project);

        string RequestBuildArtifacts(string csrfToken, int buildId);

        string RequestBuildStatus(string csrfToken, int buildId);

        string RequestCsrfToken();
    }
}