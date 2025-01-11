create table ResourceTypes (
    ResourceTypeId int not null identity,
    constraint PK_ResourceTypes PRIMARY key (ResourceTypeId),
    Name nvarchar(400) not null,
    DefaultStaff int not null constraint DF_ResourceTypes_DefaultStaff DEFAULT 1,
    Inactive bit not null CONSTRAINT DF_ResourceTypes_Inactive DEFAULT 0,
    NotificationMessage nvarchar(max) null
)
