# ZSync
Command Line for Microsoft Sync Framework

Build is x86, but can be changed to 64 by using the 64 bit version of the Microsoft Sync Framework

For more information on Microsoft Sync Framework, go here: https://msdn.microsoft.com/en-us/library/mt763482.aspx

Examples:

Copy from folder A: to Folder B:
zsync -f c:\A -t c:\B

Synchronise betweek folder A: and Folder B:
zsync -f c:\A -t c:\B -b

Copy bmp files only
zsync -f c:\A -t c:\B -i *.bmp

Get help
zsync -?

