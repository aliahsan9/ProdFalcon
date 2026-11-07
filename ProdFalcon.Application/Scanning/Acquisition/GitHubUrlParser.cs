namespace ProdFalcon.Application.Scanning.Acquisition;

public sealed record GitHubRepositoryReference(string Owner, string Name, string? Branch);

public static class GitHubUrlParser
{
    public static bool TryParse(string? input, out GitHubRepositoryReference? reference, out string error)
    {
        reference = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "A GitHub repository URL is required.";
            return false;
        }

        var trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "Enter a full https://github.com/owner/repository URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only https GitHub URLs are accepted.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Repository URLs must not include credentials.";
            return false;
        }

        var host = uri.IdnHost;
        if (!string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "www.github.com", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only public github.com repository URLs are supported.";
            return false;
        }

        if (IsBlockedHost(host))
        {
            error = "That destination is not allowed.";
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "The URL must include an owner and repository name.";
            return false;
        }

        var owner = parts[0];
        var name = parts[1];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        if (!IsValidName(owner) || !IsValidName(name))
        {
            error = "Owner and repository names contain unsupported characters.";
            return false;
        }

        string? branch = null;
        if (parts.Length >= 4 &&
            (parts[2].Equals("tree", StringComparison.OrdinalIgnoreCase) ||
             parts[2].Equals("blob", StringComparison.OrdinalIgnoreCase)))
        {
            if (parts.Length > 4)
            {
                error = "Use the repository URL and set the branch separately when the branch name contains extra path segments.";
                return false;
            }

            branch = Uri.UnescapeDataString(parts[3]);
            if (!IsValidBranch(branch))
            {
                error = "The branch name is not valid.";
                return false;
            }
        }
        else if (parts.Length > 2)
        {
            error = "Use a repository URL such as https://github.com/owner/repository.";
            return false;
        }

        reference = new GitHubRepositoryReference(owner, name, branch);
        return true;
    }

    public static Uri BuildPublicArchiveUri(string owner, string name, string branch)
    {
        if (!IsValidName(owner) || !IsValidName(name) || !IsValidBranch(branch))
            throw new ArgumentException("Repository coordinates are not valid.");

        var encodedBranch = string.Join("/", branch.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"https://codeload.github.com/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/zip/refs/heads/{encodedBranch}");
    }

    public static bool IsValidBranch(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch) || branch.Length > 250)
            return false;

        if (branch.Contains("..", StringComparison.Ordinal) || branch.Contains('\\') || branch.Contains('?') || branch.Contains('#'))
            return false;

        return branch.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '/');
    }

    private static bool IsValidName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
            return false;

        if (value is "." or "..")
            return false;

        return value.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        if (System.Net.IPAddress.TryParse(host, out _))
            return true;

        return false;
    }
}
