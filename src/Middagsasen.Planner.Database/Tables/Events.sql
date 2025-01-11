create table Events (
    EventId int not null identity,
    constraint PK_Events PRIMARY key (EventId), 
    Name nvarchar(400) not null,
    StartTime datetime not null,
    EndTime datetime not null,
    Description nvarchar(max) null,
)