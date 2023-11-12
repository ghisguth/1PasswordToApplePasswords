using System.Globalization;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using Newtonsoft.Json;

if (!File.Exists("data/data.1pif"))
{
	Console.WriteLine("data/data.1pif not found.");
	return;
}

var lines = File.ReadAllLines("data/data.1pif");

var resultData = new List<Login>();

foreach (var line in lines)
{
	if (line.StartsWith("***"))
	{
		continue;
	}

	var secureLogin = JsonConvert.DeserializeObject<SourceLogin>(line)!;

	var logins = ConvertLogin(secureLogin);
	resultData.AddRange(logins);
}

resultData = resultData.OrderBy(s => s.Title).ToList();

var csvConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
{
	HasHeaderRecord = true,
	Delimiter = ",",
	Encoding = Encoding.UTF8,
	NewLine = Environment.NewLine,
};

using (var mem = new MemoryStream())
using (var writer = new StreamWriter(mem))
using (var csvWriter = new CsvWriter(writer, csvConfig))
{
	csvWriter.WriteRecords(resultData);

	writer.Flush();
	var result = Encoding.UTF8.GetString(mem.ToArray());
	File.WriteAllText("data/result.csv", result);
	Console.WriteLine("{0} records written into data/result.csv.", resultData.Count);
}


Login[] ConvertLogin(SourceLogin sourceLogin)
{
	var username = sourceLogin.secureContents.fields.FirstOrDefault(s => s.designation == "username")?.value;
	var password = sourceLogin.secureContents.fields.FirstOrDefault(s => s.designation == "password")?.value;

	if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
	{
		Console.WriteLine("username or password is empty!!!!!");
		Dump(sourceLogin);
		return Array.Empty<Login>();
	}

	var urls = sourceLogin.secureContents.URLs
	  .Select(s => s.url)
	  .Select(s => s.StartsWith("https://", true, null) || s.StartsWith("http://", true, null) ? s : "https://" + s)
	  .Select(s => new Uri(s))
	  .Select(s => s.Scheme + "://" + s.DnsSafeHost)
	  .Distinct()
	  .ToList();

	var mainUrls = urls.Where(s => s.EndsWith(".com"))
	  .Select(s => new Uri(s))
	  .Where(s => s.DnsSafeHost.Split('.').Length > 2)
	  .Where(s => !s.DnsSafeHost.StartsWith("www."))
	  .Select(s => s.Scheme + "://" + (string.Join(".", s.DnsSafeHost.Split('.').Skip(s.DnsSafeHost.Split('.').Length - 2))))
	  .ToArray();

	foreach (var mainUrl in mainUrls)
	{
		urls.Add(mainUrl);
	}

	urls = urls.Distinct().ToList();

	if (urls.Count == 0)
	{
		Console.WriteLine("url is empty!!!!!");
		Dump(sourceLogin);
		return Array.Empty<Login>();
	}

	var notes = sourceLogin.secureContents.notesPlain;
	var notesSb = new StringBuilder(notes);

	if (notesSb.Length > 0)
	{
		notesSb.AppendLine();
		notesSb.AppendLine();
	}

	var otpAuth = string.Empty;

	foreach (var section in sourceLogin.secureContents.sections)
	{
		var sb = new StringBuilder();

		foreach (var kv in section.fields)
		{
			if (string.IsNullOrWhiteSpace(kv.t) && string.IsNullOrWhiteSpace(kv.v))
			{
				continue;
			}

			if (kv.t == "one-time password" || kv.v.StartsWith("otpauth://"))
			{
				otpAuth = kv.v;
				continue;
			}

			if (sb.Length > 0)
			{
				sb.AppendLine();
			}

			sb.AppendFormat("{0}: {1}", kv.t, kv.v);
		}

		if (sb.Length > 0)
		{
			if (!string.IsNullOrWhiteSpace(section.title))
			{
				notesSb.Append("--- ");
				notesSb.Append(section.title);
				notesSb.Append(" ---");
			}
			else
			{
				if (notesSb.Length > 0)
				{
					notesSb.Append("---");
				}
			}
			if (notesSb.Length > 0)
			{
				notesSb.AppendLine();
			}

			notesSb.Append(sb);
		}
	}

	var notesUpdates = notesSb.ToString().Trim();

	var result = new List<Login>();
	foreach (var url in urls)
	{
		var uri = new Uri(url);
		var domainName = uri.DnsSafeHost!;

		var title = $"{domainName} ({username})";
		var value = new Login
		{
			Title = title,
			URL = uri.AbsoluteUri,
			Username = username,
			Password = password,
			Notes = notesUpdates,
			OTPAuth = otpAuth,
		};
		result.Add(value);
	}

	return result.ToArray();
}

void Dump(object obj)
{
	var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
	Console.WriteLine(json);
}

class SourceLogin
{
	public string title { get; set; } = string.Empty;
	public string location { get; set; } = string.Empty;
	public string locationKey { get; set; } = string.Empty;
	public SecureContents secureContents { get; set; } = null!;
}

class SecureContents
{
	public SecureField[] fields { get; set; } = Array.Empty<SecureField>();
	public SecureSections[] sections { get; set; } = Array.Empty<SecureSections>();
	public SecureUrl[] URLs { get; set; } = Array.Empty<SecureUrl>();

	public string notesPlain { get; set; } = string.Empty;
}

class SecureField
{
	public string value { get; set; } = string.Empty;
	public string designation { get; set; } = string.Empty;
}

class SecureSections
{
	public string title { get; set; } = string.Empty;
	public SecureSectionsField[] fields { get; set; } = Array.Empty<SecureSectionsField>();
}

class SecureSectionsField
{
	public string v { get; set; } = string.Empty;
	public string t { get; set; } = string.Empty;
}

class SecureUrl
{
	public string label { get; set; } = string.Empty;
	public string url { get; set; } = string.Empty;
}


class Login
{
	public string Title { get; set; } = string.Empty;
	public string URL { get; set; } = string.Empty;
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string Notes { get; set; } = string.Empty;
	public string OTPAuth { get; set; } = string.Empty;
}
