using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TimezoneTable
{
    /// <summary>
    /// Helper to capture information about the DST change
    /// </summary>
    public record DstTransitionInfo(DateTime DstTransitionDateTime, TimeSpan OldUtcOffset, TimeSpan NewUtcOffset);


    /// <summary>
    /// All the logic for the timezone work.
    /// </summary>
    public static class DstTransitionCalculator
    {
        // Get all timezones for a specific timezone
        public static List<Tuple<DstTransitionInfo, DstTransitionInfo?>> GetTimezonesForYear(int startYear, int endYear, TimeZoneInfo theTimezoneInfo)
        {
            List<Tuple<DstTransitionInfo, DstTransitionInfo?>> zones = new();

            // Check each day since 2013 to find DST transitions
            List<DstTransitionInfo> transitionDateTimes = new();
            DateTime? transitionDateTime = null;

            transitionDateTime = GetNextTransition(new DateTime(startYear, 1, 1), theTimezoneInfo);
            if (transitionDateTime == null)
            {
                Debug.Write($"No next transition found for it on {new DateTime(startYear, 1, 1)}.");
            }
            else
            {
                while (transitionDateTime.HasValue && transitionDateTime.Value.Year <= endYear)
                {
                    var oldOffset = theTimezoneInfo.GetUtcOffset(transitionDateTime.Value.AddHours(-6).ToUniversalTime());
                    var newOffset = theTimezoneInfo.GetUtcOffset(transitionDateTime.Value.AddHours(6).ToUniversalTime());

                    transitionDateTimes.Add(new DstTransitionInfo(transitionDateTime.Value, oldOffset, newOffset));
                    transitionDateTime = GetNextTransition(transitionDateTime.Value.AddDays(1), theTimezoneInfo);
                }

                // When we collected all transition dates, order them per year
                if (transitionDateTimes.Count > 0)
                {
                    // NOTE: If there is only 1 transition available for a year, this will result in a tuple with that same date in it for both the start and finish.
                    // Make sure to handle those cases correctly when creating a file from them.
                    var yearGroups = transitionDateTimes
                        .GroupBy(x => new { x.DstTransitionDateTime.Year })
                        .Select(group => new Tuple<DstTransitionInfo, DstTransitionInfo?>(group.First(), group.Last()));

                    zones.AddRange(yearGroups);
                }
            }
            return zones;
        }

        #region Support routines

        static DateTime? GetNextTransition(DateTime asOfTime, TimeZoneInfo timeZone)
        {
            TimeZoneInfo.AdjustmentRule[] adjustments = timeZone.GetAdjustmentRules();
            if (adjustments.Length == 0)
            {
                // if no adjustment then no transition date exists
                return null;
            }

            int year = asOfTime.Year;
            TimeZoneInfo.AdjustmentRule? adjustment = null;
            foreach (TimeZoneInfo.AdjustmentRule adj in adjustments)
            {
                // Determine if this adjustment rule covers year desired
                if (adj.DateStart.Year <= year && adj.DateEnd.Year >= year)
                {
                    adjustment = adj;
                    break;
                }
            }

            if (adjustment == null)
            {
                // no adjustment found so no transition date exists in the range
                return null;
            }

            DateTime dtAdjustmentStart = GetAdjustmentDate(adjustment.DaylightTransitionStart, year);
            DateTime dtAdjustmentEnd = GetAdjustmentDate(adjustment.DaylightTransitionEnd, year);

            if (dtAdjustmentStart >= asOfTime)
            {
                // if adjusment start date is greater than asOfTime date then this should be the next transition date
                return dtAdjustmentStart;
            }
            else if (dtAdjustmentEnd >= asOfTime)
            {
                // otherwise adjustment end date should be the next transition date
                return dtAdjustmentEnd;
            }
            else
            {
                // then it should be the next year's DaylightTransitionStart

                year++;
                foreach (TimeZoneInfo.AdjustmentRule adj in adjustments)
                {
                    // Determine if this adjustment rule covers year desired
                    if (adj.DateStart.Year <= year && adj.DateEnd.Year >= year)
                    {
                        adjustment = adj;
                        break;
                    }
                }

                dtAdjustmentStart = GetAdjustmentDate(adjustment.DaylightTransitionStart, year);
                return dtAdjustmentStart;
            }
        }


        static DateTime GetAdjustmentDate(TimeZoneInfo.TransitionTime transitionTime, int year)
        {
            if (transitionTime.IsFixedDateRule)
            {
                return new DateTime(year, transitionTime.Month, transitionTime.Day);
            }
            else
            {
                // For non-fixed date rules, get local calendar
                Calendar cal = CultureInfo.CurrentCulture.Calendar;
                // Get first day of week for transition
                // For example, the 3rd week starts no earlier than the 15th of the month
                int startOfWeek = transitionTime.Week * 7 - 6;
                // What day of the week does the month start on?
                int firstDayOfWeek = (int)cal.GetDayOfWeek(new DateTime(year, transitionTime.Month, 1));
                // Determine how much start date has to be adjusted
                int transitionDay;
                int changeDayOfWeek = (int)transitionTime.DayOfWeek;

                if (firstDayOfWeek <= changeDayOfWeek)
                    transitionDay = startOfWeek + (changeDayOfWeek - firstDayOfWeek);
                else
                    transitionDay = startOfWeek + (7 - firstDayOfWeek + changeDayOfWeek);

                // Adjust for months with no fifth week
                if (transitionDay > cal.GetDaysInMonth(year, transitionTime.Month))
                    transitionDay -= 7;

                return new DateTime(year, transitionTime.Month, transitionDay, transitionTime.TimeOfDay.Hour, transitionTime.TimeOfDay.Minute, transitionTime.TimeOfDay.Second);
            }
        }

        #endregion

        #region Write to CSV file

        /// <summary>
        /// Produce a CSV file that has 1 line for each timespan of a specific timezone:
        /// <timezone_string>    <current offset>    <valid_from>  <new_offset>  <valid_to>
        /// Valid from = local time timestamp including timezone, of a specific DST change moment
        ///
        /// All regions that have just one single timezone must also be in here
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="zones"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CreateAsciiCsvFile(string fileName, IEnumerable<KeyValuePair<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>>>? zones)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            var transformedZones = TransformToRanges(zones);

            FileInfo fi = new FileInfo(fileName);
            FileStream fs = fi.Open(FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, Encoding.ASCII);

            sw.WriteLine($"timezone\tcurrent_offset\tvalid_from\tnew_offset\tvalid_until"); // If values are equal: only write first item
            try
            {
                foreach (var x in transformedZones)
                {
                    string? timestampFrom, timestampUntil = null;
                    foreach (var y in x.Value)
                    {
                        if (y.Item1 == null && y.Item2 != null)
                        {
                            timestampUntil = FormatTimestamp(y.Item2);
                            sw.WriteLine($"{x.Key}\t\t\t\t{timestampUntil}");
                        }
                        else if (y.Item1 != null && y.Item2 != null)
                        {
                            // Create a separate line for each DST transition.
                            // Skip the line if the from and until are equal.
                            if (y.Item1.DstTransitionDateTime == y.Item2.DstTransitionDateTime) continue;

                            timestampFrom = FormatTimestamp(y.Item1);
                            timestampUntil = FormatTimestamp(y.Item2);
                            sw.WriteLine($"{x.Key}\t{y.Item1.OldUtcOffset}\t{timestampFrom}\t{y.Item1.NewUtcOffset}\t{timestampUntil}");
                        }
                        else if (y.Item1 != null && y.Item2 == null)
                        {
                            timestampFrom = FormatTimestamp(y.Item1);
                            sw.WriteLine($"{x.Key}\t{y.Item1.OldUtcOffset}\t{timestampFrom}\t{y.Item1.NewUtcOffset}\t");
                        }
                        else
                            throw new NotSupportedException("Both from and until date for a DST transition are null.");
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                sw.Close();
            }
        }

        /// <summary>
        /// Transform the list of tuples to a set of ranges.
        /// </summary>
        /// <param name="zones">A dictionary with 1 item for each zone. The Value of that item contains a list of tuples. Each tuple represents 2 DST transitions of the same year.</param>
        /// <returns>Same structure but content is rearranged: each item now represents a timespan for each period of a specific summer or wintertime. First item of the tuple contains the start datetime of a timezone, 2nd item contains the END time of that same </returns>
        internal static Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> TransformToRanges(IEnumerable<KeyValuePair<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>>>? zones)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));

            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> output = new();
            foreach (var zone in zones)
            {
                // Loop through the list where each item is a pair of 2 DST transitions of a year.
                var itemsOfZone = new List<Tuple<DstTransitionInfo, DstTransitionInfo?>>();
                var transitions = zone.Value;

                DstTransitionInfo? endOfPreviousRange = null;
                foreach (var dst in transitions)
                {
                    if (dst.Item1 == null) throw new InvalidOperationException("First item cannot be null.");

                    if (endOfPreviousRange != null)
                    {
                        itemsOfZone.Add(new(endOfPreviousRange, dst.Item1));
                    }
                    itemsOfZone.Add(new(dst.Item1, dst.Item2));
                    endOfPreviousRange = dst.Item2;
                }

                //// If needed: create an items at the edges
                //if (createEdgeItems && transitions.Count > 0)
                //{
                //    itemsOfZone.Insert(0, new(null, transitions.First().Item1));     // Add timezone range for everything BEFORE the first zone
                //    if (transitions.Last().Item2 != null)
                //    {
                //        itemsOfZone.Add(new(transitions.Last().Item2, null));        // and for AFTER the last zone.
                //    }
                //}
                output.Add(zone.Key, itemsOfZone);
            }
            return output;
        }

        static string FormatTimestamp(DstTransitionInfo dstInfo)
        {
            TimeSpan zeroTimespan = new TimeSpan(0, 0, 0);

            var timestamp = $"{dstInfo.DstTransitionDateTime:yyyy'-'MM'-'dd'T'HH':'mm':'ss}";
            if (dstInfo.OldUtcOffset > zeroTimespan) timestamp += $"+"; // Add an explicit + sign inbetween
            timestamp += $"{dstInfo.OldUtcOffset}";
            return timestamp;
        }

        #endregion
    }
}
