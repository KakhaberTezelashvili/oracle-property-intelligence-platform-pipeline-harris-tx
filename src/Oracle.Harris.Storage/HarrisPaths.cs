namespace Oracle.Harris.Storage;

public static class HarrisPaths
{
    /// <summary>
    /// Resolves a repo-relative path (e.g. data/harris-tx/harris-tx.duckdb) from the directory that contains Oracle.Harris.sln,
    /// so CLI and Web agree even when the process cwd is src/Oracle.Harris.Web.
    /// </summary>
    public static string ResolveFromRepositoryRoot(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Oracle.Harris.sln")))
            {
                return Path.GetFullPath(Path.Combine(dir.FullName, relativeOrAbsolutePath));
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(relativeOrAbsolutePath);
    }
}