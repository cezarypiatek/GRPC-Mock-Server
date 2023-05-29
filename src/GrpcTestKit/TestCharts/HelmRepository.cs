namespace TestCharts;

public class HelmRepository
{
    public string Url { get; }
    public string Login { get; }
    public string Password { get; }

    public HelmRepository(string url, string login, string password)
    {
        Url = url;
        Login = login;
        Password = password;
    }
}