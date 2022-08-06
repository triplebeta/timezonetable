namespace TimezoneTable.Test
{
    public class DstTransitionCalculatorTest
    {
        private DateTime switchTo2022SummertimeDate = new DateTime(2022, 3, 25, 2, 0, 0, DateTimeKind.Local);    // 02:00 AM on March 25th 2022
        private DateTime switchTo2022WintertimeDate = new DateTime(2022, 10, 14, 3, 0, 0, DateTimeKind.Local);   // 03:00 AM on October 14th 2022
        private DateTime switchTo2023SummertimeDate = new DateTime(2023, 3, 27, 2, 0, 0, DateTimeKind.Local);
        private DateTime switchTo2023WintertimeDate = new DateTime(2023, 10, 19, 3, 0, 0, DateTimeKind.Local);

        private DstTransitionInfo transition2022Summertime, transition2022Wintertime,
                                  transition2023Summertime, transition2023Wintertime;

        public DstTransitionCalculatorTest()
        {
            transition2022Summertime = new(switchTo2022SummertimeDate, new TimeSpan(2, 0, 0), new TimeSpan(3, 0, 0));
            transition2022Wintertime = new(switchTo2022WintertimeDate, new TimeSpan(3, 0, 0), new TimeSpan(2, 0, 0));

            transition2023Summertime = new(switchTo2023SummertimeDate, new TimeSpan(2, 0, 0), new TimeSpan(3, 0, 0));
            transition2023Wintertime = new(switchTo2023WintertimeDate, new TimeSpan(3, 0, 0), new TimeSpan(2, 0, 0));
        }

        [Fact]
        public void Empty_timezone_list_transforms_to_empty_list()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> emptyList = new();
            
            var result = DstTransitionCalculator.TransformToRanges(emptyList);
            Assert.Empty(result);
        }

        [Fact]
        public void Zone_with_empty_transition_list_transforms_to_empty_list()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> singleZoneList = new();
            singleZoneList.Add("MyZone", new());

            var result = DstTransitionCalculator.TransformToRanges(singleZoneList);
            Assert.Single(result);
            var resultList = result.First().Value;
            Assert.Empty(resultList);
        }

        [Fact]
        public void Single_year_with_dst_start_and_end_returns_1_item()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> singleItemList = new();
            singleItemList.Add("MyZone", new());
            singleItemList.First().Value.Add(new(transition2022Summertime, transition2022Wintertime));

            var result = DstTransitionCalculator.TransformToRanges(singleItemList);
            Assert.Single(result);
            var resultList = result.First().Value;
            Assert.Single(resultList);

            var singleItem = resultList.First();
            Assert.Equal(switchTo2022SummertimeDate, singleItem.Item1.DstTransitionDateTime);
            Assert.Equal(DateTimeKind.Local, singleItem.Item1.DstTransitionDateTime.Kind);
            Assert.Equal(new TimeSpan(2,0,0), singleItem.Item1.OldUtcOffset);
            Assert.Equal(new TimeSpan(3, 0, 0), singleItem.Item1.NewUtcOffset);

            Assert.Equal(switchTo2022WintertimeDate, singleItem.Item2?.DstTransitionDateTime);
            Assert.Equal(DateTimeKind.Local, singleItem.Item2?.DstTransitionDateTime.Kind);
            Assert.Equal(new TimeSpan(3, 0, 0), singleItem.Item2?.OldUtcOffset);
            Assert.Equal(new TimeSpan(2, 0, 0), singleItem.Item2?.NewUtcOffset);
        }


        [Fact]
        public void Single_year_with_only_dst_start_returns_1_item()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> singleItemList = new();
            singleItemList.Add("MyZone", new());
            singleItemList.First().Value.Add(new(transition2022Summertime, null));

            var result = DstTransitionCalculator.TransformToRanges(singleItemList);
            Assert.Single(result);
            var resultList = result.First().Value;
            Assert.Single(resultList);
            var singleItem = resultList.First();
            Assert.Equal(switchTo2022SummertimeDate, singleItem.Item1.DstTransitionDateTime);
            Assert.Null(singleItem.Item2);
        }


        [Fact]
        public void Two_years_returns_3_items()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> singleItemList = new();
            List<Tuple< DstTransitionInfo, DstTransitionInfo?>> transitions = new();

            transitions.Add(new(transition2022Summertime, transition2022Wintertime));
            transitions.Add(new(transition2023Summertime, transition2023Wintertime));
            singleItemList.Add("MyZone", transitions);

            var result = DstTransitionCalculator.TransformToRanges(singleItemList);
            Assert.Single(result);

            var resultList = result.First().Value;
            Assert.Equal(3,resultList.Count);

            var item1 = resultList.First();
            Assert.Equal(switchTo2022SummertimeDate, item1.Item1.DstTransitionDateTime);
            Assert.Equal(switchTo2022WintertimeDate, item1.Item2?.DstTransitionDateTime);

            var item2 = resultList.ElementAt(1);
            Assert.Equal(switchTo2022WintertimeDate, item2.Item1.DstTransitionDateTime);
            Assert.Equal(switchTo2023SummertimeDate, item2.Item2?.DstTransitionDateTime);

            var item3 = resultList.Last();
            Assert.Equal(switchTo2023SummertimeDate, item3.Item1.DstTransitionDateTime);
            Assert.Equal(switchTo2023WintertimeDate, item3.Item2?.DstTransitionDateTime);
        }

        [Fact]
        public void Transitions_with_same_from_as_to_are_removed()
        {
            Dictionary<string, List<Tuple<DstTransitionInfo, DstTransitionInfo?>>> singleItemList = new();
            singleItemList.Add("MyZone", new());
            singleItemList.First().Value.Add(new(transition2022Summertime, null));

            var result = DstTransitionCalculator.TransformToRanges(singleItemList);
            Assert.Single(result);
            var resultList = result.First().Value;
            Assert.Single(resultList);
            var singleItem = resultList.First();
            Assert.Equal(switchTo2022SummertimeDate, singleItem.Item1.DstTransitionDateTime);
            Assert.Null(singleItem.Item2);
        }
    }
}