create table EventTemplates (
    EventTemplateId int not null identity,
    constraint PK_EventTemplates PRIMARY key (EventTemplateId), 
    Name nvarchar(400) not null,
    EventName nvarchar(400) not null,
    StartTime datetime not null,
    EndTime datetime not null,
)   