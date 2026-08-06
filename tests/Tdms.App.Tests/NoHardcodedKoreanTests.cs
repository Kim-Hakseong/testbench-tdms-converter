using System.Text.RegularExpressions;
using Xunit;

namespace Tdms.App.Tests;

/// <summary>
/// 이 앱의 기본 사용자 언어는 영어이고, 표시 문자열은 전부 사전을 거쳐야 한다.
/// 자매 앱(Modbus Workbench)에서 36개의 한글 문자열이 코드에 박힌 채 영어
/// UI에 렌더링되고 있었고 테스트는 전부 통과하고 있었다 — 화면을 눈으로 보기
/// 전에는 드러나지 않는 종류였다. 이 앱은 당시 이미 깨끗했고, 그 상태를 유지한다.
///
/// 그래서 소스를 직접 훑는다. **주석은 대상이 아니다** — 이 저장소의 주석은
/// 한국어로 쓰며, 문제는 사용자에게 보이는 문자열 리터럴이다.
/// </summary>
public sealed class NoHardcodedKoreanTests
{
    private static readonly Regex Hangul = new(@"[가-힣ㄱ-ㆎ]", RegexOptions.Compiled);

    /// <summary>
    /// 검사 제외 경로. 사전 자체는 한국어를 담는 것이 존재 이유다.
    /// </summary>
    private static readonly string[] Allowed =
    [
        Path.Combine("Tdms.App", "Localization"),
    ];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static bool IsAllowed(string path) =>
        Allowed.Any(a => path.Contains(a, StringComparison.Ordinal));

    /// <summary>C# 한 줄에서 주석을 떼어낸 앞부분만 돌려준다.</summary>
    private static string WithoutComment(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        // 줄 끝 주석. 문자열 안의 "//" 를 자르지 않도록 따옴표 밖에서만 찾는다.
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && line[i] == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    [Fact]
    public void NoKoreanInCSharpStringLiterals()
    {
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                IsAllowed(file))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = WithoutComment(lines[i]);
                foreach (Match literal in Regex.Matches(code, "\"(?:[^\"\\\\]|\\\\.)*\""))
                {
                    if (Hangul.IsMatch(literal.Value))
                    {
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}  {literal.Value.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "표시 문자열은 Localization 사전을 거쳐야 합니다. 코드에 박힌 한글:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoKoreanInXaml()
    {
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.axaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                IsAllowed(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var withoutComments = Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
            var lines = withoutComments.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (Hangul.IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}  {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "XAML의 표시 문자열도 사전 바인딩을 써야 합니다. 하드코딩된 한글:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// 위 두 검사가 실제로 한글을 잡아내는지 확인한다. 정규식이 조용히 아무것도
    /// 매치하지 않게 되면 검사는 영원히 통과하고 아무도 눈치채지 못한다.
    /// </summary>
    [Fact]
    public void DetectorActuallyMatchesKorean()
    {
        Assert.Matches(Hangul, "\"30 라인 | Err 0\"");
        Assert.DoesNotMatch(Hangul, "\"30 lines | Err 0\"");
        Assert.Equal(string.Empty, WithoutComment("        // 한글 주석은 허용된다"));
        Assert.Contains("\"ok\"", WithoutComment("var x = \"ok\"; // 뒤 주석"));
    }
}
