using System;
using System.Text.RegularExpressions;
var token = "if width >= 100 and height > 60";
var rx = new Regex("^if\s+(?<condition>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
var m = rx.Match(token);
Console.WriteLine(m.Success);
Console.WriteLine(m.Groups["condition"].Value);
