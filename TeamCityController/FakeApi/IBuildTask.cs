namespace TeamCityController.FakeApi
{
    public interface IBuildTask
    {
        string Artifacts { get; }

        string Status { get; }
    }
}