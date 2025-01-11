create table ResourceTemplates (
    ResourceTemplateId int not null identity,
    constraint PK_ResourceTemplates PRIMARY key (ResourceTemplateId),
    EventTemplateId int not null,
    constraint FK_ResourceTemplates_EventTemplates FOREIGN KEY (EventTemplateId) REFERENCES EventTemplates(EventTemplateId) on delete cascade,
    ResourceTypeId int not null,
    constraint FK_ResourceTemplates_ResourceTypes FOREIGN KEY (ResourceTypeId) REFERENCES ResourceTypes(ResourceTypeId) on delete cascade,
    StartTime datetime null,
    EndTime datetime null,
    MinimumStaff int not null,
)