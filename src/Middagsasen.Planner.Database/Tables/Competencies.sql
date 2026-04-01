create table Competencies (
    CompetencyId int not null identity,
    constraint PK_Competencies PRIMARY key (CompetencyId),
    Name nvarchar(400) not null,
    Description nvarchar(max) null,
    HasExpiry bit not null constraint DF_Competencies_HasExpiry default 0,
    Inactive bit not null constraint DF_Competencies_Inactive default 0
)
