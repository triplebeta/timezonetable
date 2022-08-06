# Timezone table generator

Tool to help you generate a table of all daylight saving time (DST) transitions for timezones. It uses the information of the Operating System and exports to a CSV file.

Note: When I ran this tool on Windows I got the exact times of the DST transitions but when running it on Ubuntu 18.04 LTS all DST times were set to 00:00:00

## How to use the tool
To create a set of CSV files with all DST transitions for 2013 to 2027 you run:
TimetableTable.exe 2013 2027

When you run this on Windows 11 you will get the output that you can also find in this repo in the Output directory.