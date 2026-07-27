namespace WindowsRdpApp.Services;

public interface ICredentialProtectionService
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
