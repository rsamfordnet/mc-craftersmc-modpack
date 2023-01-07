<Query Kind="Program">
  <NuGetReference>Markdig</NuGetReference>
  <NuGetReference>RestSharp</NuGetReference>
  <NuGetReference>Tomlyn</NuGetReference>
  <Namespace>Markdig</Namespace>
  <Namespace>System.Runtime.Serialization</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Tomlyn</Namespace>
  <Namespace>Tomlyn.Model</Namespace>
</Query>

#load "libs/packwiz.linq"
#load "libs/mod-groups-file.linq"
#load "libs/markdown-builder.linq"

async Task Main() {
	var modpack = Modpack.Open();
	var mods = await modpack.GetMods();
	var modSlugs = mods.Select(x => x.Slug).ToArray();

	var groupsFilePath = Path.Combine(modpack.DirectoryPath, "groups.yml");
	var groupsFile = await ModGroupsFile.ReadFromFile(groupsFilePath, sortMods: true);
	groupsFile.Sync(modSlugs);
	await groupsFile.Save();

	var md = new MarkdownBuilder();
	md.AppendLine($"# CraftersMC Modpack Mods");

	foreach (var modsByCategory in mods
		.GroupByModGroupName(groupsFile)
		.OrderByModGroupsFileOrder(groupsFile)
	) {
		md.AppendLine($"## {modsByCategory.Key}");

		foreach (var modsByRequired in modsByCategory.GroupBy(x => x.IsRequired).OrderByDescending(x => x.Key)) {
			md.AppendMods($"**{(modsByRequired.Key ? "Required" : "Optional")}**", modsByRequired);
		}
	}

	var markdown = md.AsMarkdown();

	var modsFilePath = Path.Combine(modpack.DirectoryPath, "MODS.md");
	await File.WriteAllTextAsync(modsFilePath, markdown);
	
	md.DumpAsHtml();
}

public static class ModSortOrder {
	public static IEnumerable<IGrouping<string, PackwizMod>> GroupByModGroupName(
		this IEnumerable<PackwizMod> mods,
		ModGroupsFile groupsFile
	) {
		return mods.GroupBy(x => groupsFile.GetByModSlug(x.Slug)?.Name ?? "Unknown");
	}
	
	public static IOrderedEnumerable<T> OrderByModGroupsFileOrder<T>(
		this IEnumerable<T> grouping,
		ModGroupsFile groupsFile
	) where T : IGrouping<string, PackwizMod> {
		return grouping.OrderBy(x => {
			var groupName = x.Key;
			var group = groupsFile.Groups.SingleOrDefault(g => g.Name == groupName);
			if (group is null) {
				return int.MaxValue;
			}

			return groupsFile.Groups.IndexOf(group);
		});
	}
}

public static class ModMarkdownBuilder {
	public static string ModpackRelativeUrl(this PackwizMod mod) {
		return mod.ModpackRelativeFilePath.Replace('\\', '/').TrimStart('/');
	}
	
	public static string ModpackGitHubUrl(this PackwizMod mod, string userOrOrganizationName, string projectName, string branchName) {
		return GitHubDirectUrl(userOrOrganizationName, projectName, branchName, mod.ModpackRelativeUrl());
	}

	private static string GitHubDirectUrl(string userOrOrganizationName, string projectName, string branchName, string relativePath) {
		return "https://github.com/" + userOrOrganizationName + "/" + projectName + "/blob/" + branchName + "/" + relativePath.TrimStart('/');
	}

	public static void AppendMods(this MarkdownBuilder md, string title, IEnumerable<PackwizMod> mods) {
		md.AppendLine(title);
		md.AppendMods(mods);
		md.AppendLine();
	}

	public static void AppendMods(this MarkdownBuilder md, IEnumerable<PackwizMod> mods) {
		foreach (var mod in mods) {
			md.AppendMod(mod);
		}
	}

	public static void AppendMod(this MarkdownBuilder md, PackwizMod mod) {
		md.Append($"* {mod.Name}");

		if (mod.FullFilePath is not null) {
			md.AppendShield(
				label: mod.Slug,
				message: ".pw.toml",
				color: "blueviolet",
				linkUrl: mod.ModpackGitHubUrl("ChristopherHaws", "mc-craftersmc-modpack", "1.19/dev"),
				altText: "packwiz"
			);
		}

		if (mod.Modrinth is not null) {
			md.AppendModrinthModShield(
				modSlug: mod.Slug,
				modId: mod.Modrinth.Value.Id,
				modUrl: mod.Modrinth.Value.Url,
				label: "",
				hoverText: "modrinth",
				logo: true,
				style: "flat",
				color: "26292f"
			);
		}

		if (mod.CurseForge is not null) {
			md.AppendCurseForgeProjectShield(
				projectSlug: mod.Slug,
				projectId: mod.CurseForge.Value.Id,
				projectUrl: mod.CurseForge.Value.Url,
				style: "short"
			);
		}

		md.AppendLine();
	}
}
