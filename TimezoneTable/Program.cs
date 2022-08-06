using System.Diagnostics;
using System.Text;
using TimezoneTable;

// Tab-Separated Values (CSV) files produced by this tool
const string timezoneTable = @"Timezone.csv";                                               // All timezones that have only 1 DST transition for all years
const string singleDstTimezonesTable = @"TimezonesWithOneDSTTime.csv";                      // All timezones that have only 1 DST transition for all years
const string mixedDstTimezonesTable = @"TimezonesWithOneOrTwoDSTTime.csv";                  // All timezones that have sometimes have only 1 but sometimes 2 DST transitions
const string timezonesWithDSTButNoTransitions = "TimezonesWithDSTButNoTransitions.csv";     // Timezones that do report to have DST but for which no transitions were found
const string timezonesWithoutDST = "TimezonesWithoutDST.csv";                               // Timezones without DST
const string timezonesNotFound = "TimezonesNotFound.csv";                                   // Invalid timezones

int startYear, endYear;

Console.WriteLine($"Timezone DST transition table generator");
Console.WriteLine($"=======================================");
Console.WriteLine($"Purpose of this tool is to create a table containing the exact DST dates and times for a set of timezones.");
Console.WriteLine($"It will create a set of tab-separated ASCII encoded files that you can then combine as you see fit.");
Console.WriteLine($"Each line contains a distinct period with a specific DST, transition times are in local time (from-until).\n");
Console.WriteLine($"IMPORTANT:");
Console.WriteLine($" * Run it on Windows, DST transition times will all be 00:00:00 when running on Linux.");
Console.WriteLine($" * The result sometimes seems to differ from the result on websites");
Console.WriteLine($" * Validate strange results using a site like: https://www.zeitverschiebung.net/en");
Console.WriteLine($"");

// Get the commandline parameters
try
{
    if (args.Length != 2) throw new ArgumentException("Missing or too many arguments.");
    if (!int.TryParse(args[0], out startYear)) throw new ArgumentException($"First parameter {args[0]} must be a year.");
    if (!int.TryParse(args[1], out endYear)) throw new ArgumentException($"Second parameter {args[0]} must be a year.");
    if (startYear > endYear) throw new ArgumentException("Start year must be <= end year.");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"ERROR: {ex.Message}.\n");
    Console.WriteLine($"Syntax: TimezoneTable.exe <startyear> <endyear>");
    return;
}

string[] allTimezones;
if (File.Exists("TimezoneList.txt"))
{
    Console.WriteLine($"Using timezones from the file TimezoneList.txt");
    allTimezones = File.ReadAllLines("TimezoneList.txt");
}
else
{
    Console.WriteLine($"TimezoneList.txt not found in the current directory, using embedded default timezone list.");
    allTimezones = TimezoneTable.TimezoneTable.DefaultTimezonesList; // Use the default list;
}
Console.Write($"Creating timezone transition table from {startYear} to {endYear} for {allTimezones.Count()} timezones...");

// Here is code to simply validate if there still is a difference between running this tool on Linux or Windows.
// var date = new DateTime(2022, 1, 1);
// Show DST change moments for Netherlands
// Note the difference in the time of the transition:
// Linux:   returns +00:00:00   (inaccurate)
// Windows  returns +02:00:00   (valid)

//var timezoneAmsterdam = "Europe/Amsterdam";
//Console.WriteLine($"{timezoneAmsterdam}\t{transitionDateNL}");
//return -1;

List<string> zonesWithNoDST = new();
List<string> zonesNotFound = new();
List<string> zonesWithDSTButNoTransitions = new();
Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> zones = new();

foreach (var zone in allTimezones)
{
    try
    {
        // Get timezone info from the system
        var theTimezoneInfo = TimeZoneInfo.FindSystemTimeZoneById(zone);
        if (theTimezoneInfo.SupportsDaylightSavingTime == false)
        {
            Debug.WriteLine($"Timezone {zone} has no DST.");
            zonesWithNoDST.Add(zone);
            continue;
        }

        // If it does support timezones: create an entry for it
        Debug.Write($"Timezone {zone} has DST, getting the transitions...");
        var newZones = DstTransitionCalculator.GetTimezonesForYear(startYear, endYear, theTimezoneInfo);
        if (newZones.Count == 0)
        {
            zonesWithDSTButNoTransitions.Add(zone);
        }
        else
        {
            zones.Add(zone, newZones);
        }
        Debug.WriteLine("");
    }
    catch (TimeZoneNotFoundException)
    {
        Debug.WriteLine($"Timezone {zone}: Not found!");
        zonesNotFound.Add(zone);
    }
} // for each zone
Console.WriteLine("Done\n");

// Report what we found

// All timezones that consistently have 2 distinct dates for DST in each year
var validZones = zones.Where(x => x.Value.All(z=>z.Item1.DstTransitionDateTime != z.Item2?.DstTransitionDateTime && z.Item1.OldUtcOffset != z.Item2?.OldUtcOffset));
Console.WriteLine($"{validZones.Select(x=>x.Key).Distinct().Count()} timezones with start and end transition written to file {timezoneTable}");
DstTransitionCalculator.CreateAsciiCsvFile(timezoneTable, validZones);

// All timezones that have at least 1 year where there is only one known DST transition
var zonesThatSometimesHaveOnlyOneDSTtransition = zones.Where(x => x.Value.Any(z => z.Item1.DstTransitionDateTime == z.Item2?.DstTransitionDateTime || z.Item1.OldUtcOffset == z.Item2?.OldUtcOffset));
var zonesWithOnlyExactlyOneDSTtransition = zonesThatSometimesHaveOnlyOneDSTtransition.Where(x => x.Value.All(z => z.Item1 == z.Item2));
var zonesWithMixedOneOrTwoDSTtransitions = zonesThatSometimesHaveOnlyOneDSTtransition.Except(zonesWithOnlyExactlyOneDSTtransition);

// Report timezones that have for every year just 1 DST transition
Console.WriteLine($"{zonesWithOnlyExactlyOneDSTtransition.Select(x => x.Key).Distinct().Count()} timezones with just one DST transition for all years written to file {singleDstTimezonesTable}");
DstTransitionCalculator.CreateAsciiCsvFile(singleDstTimezonesTable, zonesWithOnlyExactlyOneDSTtransition);

// Report timezones that have FOR SOME YEARS 2 DST transitions and for other years only 1 DST transition
Console.WriteLine($"{zonesWithMixedOneOrTwoDSTtransitions.Select(x => x.Key).Distinct().Count()} timezones that mix years of one and two DST transitions written to file {mixedDstTimezonesTable}");
DstTransitionCalculator.CreateAsciiCsvFile(mixedDstTimezonesTable, zonesWithMixedOneOrTwoDSTtransitions);

// Timezones with no timezones
Console.WriteLine($"{zonesWithDSTButNoTransitions.Count()} zones without DST written to file {timezonesWithDSTButNoTransitions}");
File.WriteAllLines(timezonesWithDSTButNoTransitions, zonesWithDSTButNoTransitions.ToArray(), Encoding.ASCII);

// Timezones with no DST
Console.WriteLine($"{zonesWithNoDST.Count()} zones without DST written to file {timezonesWithoutDST}");
File.WriteAllLines(timezonesWithoutDST, zonesWithNoDST.ToArray(), Encoding.ASCII);

// Timezones that are in the list but cannot be resolved. Perhaps due to a typo.
Console.WriteLine($"{zonesNotFound.Count()} zones not found written to file {timezonesNotFound}");
File.WriteAllLines(timezonesNotFound, zonesNotFound.ToArray(), Encoding.ASCII);

int total = validZones.Count() + zonesWithOnlyExactlyOneDSTtransition.Count() + zonesWithMixedOneOrTwoDSTtransitions.Count() + zonesWithDSTButNoTransitions.Count() + zonesNotFound.Count() + zonesWithNoDST.Count();
Console.WriteLine($"\n{total} zones processed.");