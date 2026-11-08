using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

/// <summary>
/// Line-oriented patterns for vulnerability classes that the dedicated secret and SQL analyzers do not cover.
/// A match is a static indicator, not proof that the line is reachable.
/// </summary>
public static class OwaspPatternAnalyzer
{
    private readonly record struct Pattern(
        Regex Expression,
        string Languages,
        string RuleId,
        string Title,
        string Severity,
        string Confidence,
        string Cwe,
        string Owasp,
        string Description,
        string Why,
        string Scenario,
        string Remediation,
        string Example);

    private static readonly Pattern[] Patterns =
    [
        new(new(@"\bHtml\.Raw\s*\(", RegexOptions.Compiled), "markup",
            "SEC-XSS-001", "Unencoded HTML output", "High", "Medium", "CWE-79", "OWASP A03:2021",
            "Html.Raw writes markup without encoding.",
            "If any part of the argument comes from a request, the browser will treat it as HTML.",
            "A caller can submit a script tag that runs in another user's session.",
            "Encode the value, or pass a sanitized fragment produced by a dedicated sanitizer.",
            "@Html.Encode(comment)"),
        new(new(@"dangerouslySetInnerHTML", RegexOptions.Compiled), "web",
            "SEC-XSS-001", "HTML assigned without escaping", "High", "Medium", "CWE-79", "OWASP A03:2021",
            "dangerouslySetInnerHTML inserts HTML as markup.",
            "React will not escape that markup.",
            "Stored or reflected script in the assigned object runs in the page.",
            "Render text through the normal JSX expression so React escapes it.",
            "<div>{comment}</div>"),
        new(new(@"\.innerHTML\s*=\s*[^;\n]*(\+|\$\{)", RegexOptions.Compiled), "web",
            "SEC-XSS-001", "innerHTML built from a dynamic value", "High", "Medium", "CWE-79", "OWASP A03:2021",
            "innerHTML is assigned a string that is concatenated or interpolated.",
            "The browser parses that string as HTML.",
            "A value that includes a tag or event handler executes in the page.",
            "Assign text with textContent, or escape the value before it is parsed as HTML.",
            "element.textContent = name;"),
        new(new(@"\bdocument\.write\s*\(", RegexOptions.Compiled), "web",
            "SEC-XSS-001", "document.write used to emit markup", "Medium", "Medium", "CWE-79", "OWASP A03:2021",
            "document.write parses its argument as HTML.",
            "Dynamic arguments are interpreted by the browser.",
            "A request value included in the argument can run script.",
            "Build the page with DOM APIs that assign text, not HTML.",
            "element.textContent = message;"),
        new(new(@"\bv-html\s*=", RegexOptions.Compiled), "web",
            "SEC-XSS-001", "Vue v-html renders raw markup", "Medium", "Medium", "CWE-79", "OWASP A03:2021",
            "v-html inserts the bound value as HTML.",
            "Vue does not escape that binding.",
            "Untrusted HTML in the bound value runs in the component.",
            "Use the default mustache binding, which escapes text.",
            "<div>{{ comment }}</div>"),
        new(new(@"\bProcess\.Start\s*\([^;\n]*\+", RegexOptions.Compiled), "cs",
            "SEC-CMDI-001", "Process.Start argument is concatenated", "High", "Medium", "CWE-78", "OWASP A03:2021",
            "A process is started with a concatenated argument.",
            "Metacharacters in the appended value can change what the process runs.",
            "A request value appended to a shell command can run an extra command.",
            "Pass a fixed executable and a separate argument array. Do not use the shell to build the command.",
            "Process.Start(new ProcessStartInfo(tool) { ArgumentList = { id } });"),
        new(new(@"\bnew\s+ProcessStartInfo\s*\([^;\n]*\+", RegexOptions.Compiled), "cs",
            "SEC-CMDI-001", "Process start info is built by concatenation", "High", "Medium", "CWE-78", "OWASP A03:2021",
            "The process file name or arguments are concatenated.",
            "The concatenated value is passed to the operating system.",
            "If that value includes a request parameter, the caller may change the command.",
            "Use ArgumentList with one argument per value.",
            "var info = new ProcessStartInfo(tool); info.ArgumentList.Add(id);"),
        new(new(@"\bos\.system\s*\(\s*f['""]", RegexOptions.Compiled), "py",
            "SEC-CMDI-001", "os.system called with an f-string", "High", "High", "CWE-78", "OWASP A03:2021",
            "os.system runs a shell command built with an f-string.",
            "Interpolated values are parsed by the shell.",
            "A quote in the interpolated value can add another command.",
            "Use subprocess.run with a list of arguments and shell=False.",
            "subprocess.run([tool, name], check=True)"),
        new(new(@"\bos\.system\s*\([^)]*\+", RegexOptions.Compiled), "py",
            "SEC-CMDI-001", "os.system command is concatenated", "High", "High", "CWE-78", "OWASP A03:2021",
            "os.system receives a concatenated command string.",
            "The shell parses the whole string.",
            "User input joined onto the command can run additional commands.",
            "Use subprocess.run with a list of arguments and shell=False.",
            "subprocess.run([tool, name], check=True)"),
        new(new(@"\bshell\s*=\s*True\b", RegexOptions.Compiled), "py",
            "SEC-CMDI-001", "subprocess runs through the shell", "Medium", "Medium", "CWE-78", "OWASP A03:2021",
            "A subprocess call sets shell=True.",
            "The shell interprets metacharacters in the command.",
            "Untrusted text in the command string can start another program.",
            "Pass an argument list and leave shell at its default of False.",
            "subprocess.run([tool, name], check=True)"),
        new(new(@"\bexec(?:Sync)?\s*\(\s*(?:`[^`\n]*\$\{|[^)\n]*\+)", RegexOptions.Compiled), "js",
            "SEC-CMDI-001", "Child process command is dynamic", "High", "Medium", "CWE-78", "OWASP A03:2021",
            "exec is called with a template or concatenated command.",
            "exec invokes a shell.",
            "A request value inside the command can run an extra program.",
            "Use execFile or spawn with an argument array and no shell.",
            "execFile(tool, [id], callback);"),
        new(new(@"\bRuntime\.getRuntime\(\)\.exec\s*\([^)]*\+", RegexOptions.Compiled), "java",
            "SEC-CMDI-001", "Runtime.exec command is concatenated", "High", "Medium", "CWE-78", "OWASP A03:2021",
            "Runtime.exec receives a concatenated command.",
            "The runtime tokenizes that string.",
            "Untrusted input in the string can change the program or its arguments.",
            "Use ProcessBuilder with one argument per list entry.",
            "new ProcessBuilder(tool, id).start();"),
        new(new(@"\bFile\.(?:ReadAllText|ReadAllBytes|ReadAllLines|WriteAllText|WriteAllBytes|AppendAllText|Open|OpenRead|OpenWrite|Delete|Copy|Move)\s*\([^;\n]*\+\s*[A-Za-z_]", RegexOptions.Compiled), "cs",
            "SEC-PATH-001", "File API path is concatenated", "High", "Medium", "CWE-22", "OWASP A01:2021",
            "A file path is built by appending a variable.",
            "The variable can contain .. segments or an absolute path.",
            "A request file name such as ..\\..\\secret can leave the intended directory.",
            "Combine a fixed root, reject rooted paths and .., then confirm the full path stays under that root.",
            "var full = Path.GetFullPath(Path.Combine(root, name)); if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();"),
        new(new(@"\bPath\.Combine\s*\([^)]*\b(Request|HttpContext|userInput|fileName|filename)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cs",
            "SEC-PATH-001", "Path.Combine uses request data", "Medium", "Medium", "CWE-22", "OWASP A01:2021",
            "Path.Combine is called with a name that looks like request input.",
            "Combine does not remove a rooted second argument or .. segments.",
            "An absolute path or .. in the request value can read a file outside the folder.",
            "Reject rooted values and .., then check the resolved path against the root.",
            "if (Path.IsPathRooted(name) || name.Contains(\"..\")) return BadRequest();"),
        new(new(@"\bsendFile\s*\([^)]*\b(req|request)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "js",
            "SEC-PATH-001", "sendFile uses the request path", "High", "Medium", "CWE-22", "OWASP A01:2021",
            "A file response is built from the request.",
            "The requested path is not shown being confined to one directory.",
            "A path such as ../secret can be returned to the caller.",
            "Resolve the path and verify it stays inside a fixed root before sending it.",
            "const full = path.resolve(root, name); if (!full.startsWith(root)) return res.sendStatus(400);"),
        new(new(@"\bnew\s+BinaryFormatter\b|\bBinaryFormatter\s*\(", RegexOptions.Compiled), "cs",
            "SEC-DESER-001", "BinaryFormatter is used", "Critical", "High", "CWE-502", "OWASP A08:2021",
            "BinaryFormatter can instantiate types named in the payload.",
            "The payload chooses the types that get created.",
            "A crafted payload can run code during deserialization.",
            "Delete BinaryFormatter usage. Deserialize a fixed DTO with System.Text.Json.",
            "var dto = JsonSerializer.Deserialize<ReportDto>(json);"),
        new(new(@"\bTypeNameHandling\.(All|Auto|Objects)\b", RegexOptions.Compiled), "cs",
            "SEC-DESER-001", "JSON type names are honored", "High", "High", "CWE-502", "OWASP A08:2021",
            "Newtonsoft.Json is configured to read type names from the payload.",
            "The payload can name a type to construct.",
            "A type name in untrusted JSON can lead to code execution.",
            "Leave TypeNameHandling at None and deserialize a known type.",
            "JsonConvert.DeserializeObject<ReportDto>(json);"),
        new(new(@"\bpickle\.loads\s*\(", RegexOptions.Compiled), "py",
            "SEC-DESER-001", "pickle.loads deserializes a byte stream", "High", "High", "CWE-502", "OWASP A08:2021",
            "pickle.loads reconstructs objects from a stream.",
            "The stream can name callables to run.",
            "Untrusted pickle data can execute code on load.",
            "Reject pickle for untrusted input. Use json.loads for data.",
            "payload = json.loads(raw)"),
        new(new(@"\byaml\.load\s*\(", RegexOptions.Compiled), "py",
            "SEC-DESER-001", "yaml.load without a safe loader", "Medium", "Medium", "CWE-502", "OWASP A08:2021",
            "yaml.load is called without SafeLoader on the same line.",
            "The default loader can construct arbitrary objects.",
            "A YAML document from a user can execute code when it is loaded.",
            "Use yaml.safe_load.",
            "data = yaml.safe_load(raw)"),
        new(new(@"\bnew\s+ObjectInputStream\b", RegexOptions.Compiled), "java",
            "SEC-DESER-001", "Java ObjectInputStream deserializes objects", "High", "Medium", "CWE-502", "OWASP A08:2021",
            "ObjectInputStream reads a Java serialized object graph.",
            "The stream selects the classes to create.",
            "Untrusted serialized data can lead to gadget-chain execution.",
            "Do not accept Java serialization from callers. Read a JSON DTO instead.",
            "objectMapper.readValue(json, ReportDto.class);"),
        new(new(@"\bunserialize\s*\(", RegexOptions.Compiled), "php",
            "SEC-DESER-001", "PHP unserialize on a string", "High", "Medium", "CWE-502", "OWASP A08:2021",
            "unserialize rebuilds PHP values from a string.",
            "Object payloads can trigger magic methods.",
            "Untrusted serialized input can change control flow.",
            "Use json_decode for untrusted data.",
            "$data = json_decode($raw, true);"),
        new(new(@"\b(MD5|SHA1)\.(Create|HashData)\s*\(|\bnew\s+(MD5|SHA1)(CryptoServiceProvider|Managed)\b", RegexOptions.Compiled), "cs",
            "SEC-CRYPTO-001", "Weak hash algorithm", "Medium", "Medium", "CWE-327", "OWASP A02:2021",
            "MD5 or SHA-1 is used.",
            "Both algorithms are cheap to collide and unsuitable for passwords or signatures.",
            "A collision or a fast guess can defeat an integrity or password check that depends on this hash.",
            "Use SHA-256 or stronger for integrity, and a password hasher such as PBKDF2, scrypt, or Argon2 for passwords.",
            "var hash = SHA256.HashData(bytes);"),
        new(new(@"\b(DES|TripleDES)\.(Create)\s*\(|\bnew\s+(DES|TripleDES)CryptoServiceProvider\b|\bCipherMode\.ECB\b", RegexOptions.Compiled), "cs",
            "SEC-CRYPTO-001", "Weak or unauthenticated cipher mode", "High", "High", "CWE-327", "OWASP A02:2021",
            "DES, TripleDES, or ECB mode is used.",
            "DES is obsolete, and ECB repeats the same ciphertext for the same block.",
            "An attacker can recover patterns or brute-force old keys more easily than with AES-GCM.",
            "Use Aes with an authenticated mode such as GCM, and a random nonce per message.",
            "using var aes = new AesGcm(key, 16);"),
        new(new(@"\bnew\s+Random\s*\([^)]*\)", RegexOptions.Compiled), "cs",
            "SEC-CRYPTO-001", "Non-cryptographic random used beside a secret", "Medium", "Low", "CWE-338", "OWASP A02:2021",
            "System.Random is constructed on a line that also mentions a secret, token, password, or key.",
            "System.Random is predictable.",
            "A token or password generated this way can be guessed.",
            "Use RandomNumberGenerator for security-sensitive values.",
            "var bytes = RandomNumberGenerator.GetBytes(32);"),
        new(new(@"\b(password|passwd|pwd)\b[^;\n]{0,80}\b(MD5|SHA1)\b|\b(MD5|SHA1)\b[^;\n]{0,80}\b(password|passwd|pwd)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "any",
            "SEC-PWDHASH-001", "Password hashed with MD5 or SHA-1", "High", "Medium", "CWE-916", "OWASP A02:2021",
            "A password and MD5 or SHA-1 appear in the same statement.",
            "Those hashes are too fast for password storage.",
            "A leaked hash can be guessed offline with commodity hardware.",
            "Store passwords with a slow adaptive hasher and a unique salt.",
            "var hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 210_000, 32));"),
        new(new(@"\beval\s*\(", RegexOptions.Compiled), "js",
            "SEC-CMDI-001", "eval executes a dynamic string", "Medium", "Low", "CWE-95", "OWASP A03:2021",
            "eval compiles and runs a string.",
            "The string is treated as code.",
            "If any part of the string is attacker-controlled, it runs as script.",
            "Replace eval with a direct call or JSON.parse for data.",
            "const data = JSON.parse(raw);")
    ];

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var extension = Path.GetExtension(filePath);
        if (!IsCode(extension))
            return issues;

        if (filePath.Contains(".min.", StringComparison.OrdinalIgnoreCase))
            return issues;

        var lines = content.Split('\n');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (IsComment(trimmed, extension))
                continue;

            if (trimmed.Contains("yaml.load", StringComparison.Ordinal) &&
                trimmed.Contains("SafeLoader", StringComparison.Ordinal))
                continue;

            foreach (var pattern in Patterns)
            {
                if (!Applies(pattern.Languages, extension))
                    continue;

                if (pattern.RuleId == "SEC-CRYPTO-001" &&
                    pattern.Title.Contains("Non-cryptographic", StringComparison.Ordinal) &&
                    !MentionsSecret(line))
                    continue;

                if (!pattern.Expression.IsMatch(line))
                    continue;

                var key = pattern.RuleId + "|" + pattern.Title;
                if (!seen.Add(key))
                    continue;

                issues.Add(new ScanIssue
                {
                    RuleId = pattern.RuleId,
                    RuleName = nameof(OwaspPatternRule),
                    Title = pattern.Title,
                    Severity = pattern.Severity,
                    Category = Category(pattern.RuleId),
                    FilePath = filePath,
                    LineNumber = i + 1,
                    Confidence = pattern.Confidence,
                    Cwe = pattern.Cwe,
                    DetectionMethod = "Static analysis",
                    Description = pattern.Description,
                    Impact = pattern.Why + " " + pattern.Scenario,
                    Remediation = pattern.Remediation,
                    SecureExample = pattern.Example,
                    Evidence = Snippet(line),
                    References = pattern.Owasp + " https://owasp.org/Top10/",
                    Status = "Open"
                });
            }
        }

        return issues;
    }

    private static bool MentionsSecret(string line) =>
        line.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("apiKey", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("nonce", StringComparison.OrdinalIgnoreCase);

    private static bool IsCode(string extension) =>
        extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".java", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".php", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".vue", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".html", StringComparison.OrdinalIgnoreCase);

    private static bool Applies(string languages, string extension) => languages switch
    {
        "any" => true,
        "cs" => Is(extension, ".cs", ".cshtml"),
        "py" => Is(extension, ".py"),
        "java" => Is(extension, ".java"),
        "php" => Is(extension, ".php"),
        "js" => Is(extension, ".js", ".jsx", ".ts", ".tsx", ".vue"),
        "web" => Is(extension, ".js", ".jsx", ".ts", ".tsx", ".vue", ".html", ".cshtml"),
        "markup" => Is(extension, ".cshtml", ".cs", ".html"),
        _ => false
    };

    private static bool Is(string extension, params string[] candidates) =>
        candidates.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));

    private static bool IsComment(string trimmed, string extension)
    {
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith('*') ||
            trimmed.StartsWith("/*", StringComparison.Ordinal))
            return true;

        if (extension.Equals(".py", StringComparison.OrdinalIgnoreCase) && trimmed.StartsWith('#'))
            return true;

        return trimmed.StartsWith("--", StringComparison.Ordinal);
    }

    private static string Category(string ruleId) => ruleId switch
    {
        "SEC-CRYPTO-001" or "SEC-PWDHASH-001" => "Cryptography",
        "SEC-PATH-001" => "Files",
        "SEC-DESER-001" => "Deserialization",
        _ => "Injection"
    };

    private static string Snippet(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= 180 ? trimmed : trimmed[..180] + "...";
    }
}
