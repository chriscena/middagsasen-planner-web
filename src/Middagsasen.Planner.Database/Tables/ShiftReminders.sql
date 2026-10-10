create table ShiftReminders (
    ShiftReminderId int not null IDENTITY,
    CONSTRAINT PK_ShiftReminders PRIMARY KEY (ShiftReminderId),
    UserId int not null,
    CONSTRAINT FK_ShiftReminders_Users FOREIGN KEY (UserId) REFERENCES Users (UserId),
    ShiftDate date not null,
    SentTime datetime not null,
    Success bit not null,
    Info nvarchar(max) null
)
GO

-- Høyst én påminnelse per bruker per vaktdag; raden er både dedup og logg.
CREATE UNIQUE INDEX IX_ShiftReminders_UserId_ShiftDate ON ShiftReminders (UserId, ShiftDate)
