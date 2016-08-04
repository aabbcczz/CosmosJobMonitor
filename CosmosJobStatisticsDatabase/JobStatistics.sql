CREATE TABLE [dbo].[JobStatistics]
(
	[Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, 
    [Name] NVARCHAR(MAX) NOT NULL, 
	[HyperLink] NVARCHAR(MAX) NOT NULL,
    [UserName] NVARCHAR(50) NOT NULL, 
    [TrueUserName] NVARCHAR(50) NOT NULL,
    [State] INT NOT NULL, 
    [SubmitTime] DATETIME NULL, 
    [StartTime] DATETIME NULL, 
    [EndTime] DATETIME NULL, 
    [TotalRunningTimeInSecond] BIGINT NULL DEFAULT 0, 
    [PNSeconds] BIGINT NULL DEFAULT 0, 
)

GO

CREATE INDEX [IX_JobStatistics_UserName] ON [dbo].[JobStatistics] ([TrueUserName])

GO


CREATE INDEX [IX_JobStatistics_StatePNSeconds] ON [dbo].[JobStatistics] ([State], [PNSeconds])

GO

CREATE INDEX [IX_JobStatistics_SubmitTime] ON [dbo].[JobStatistics] ([SubmitTime])

GO
