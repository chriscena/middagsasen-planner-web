create table EventResources (
    EventResourceId int not null identity,
    constraint PK_EventResources PRIMARY key (EventResourceId),
    EventId int not null,
    constraint FK_EventResources_Events FOREIGN KEY (EventId) REFERENCES Events(EventId) on delete cascade,
    ResourceTypeId int not null,
    constraint FK_EventResources_ResourceTypes FOREIGN KEY (ResourceTypeId) REFERENCES ResourceTypes(ResourceTypeId) on delete cascade,
    StartTime datetime null,
    EndTime datetime null,
    MinimumStaff int not null,
)