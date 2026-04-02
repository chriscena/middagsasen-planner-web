CREATE TABLE [dbo].[ResourceTypeCompetencies]
(
  ResourceTypeCompetencyId int not null identity,
  constraint PK_ResourceTypeCompetencies PRIMARY KEY (ResourceTypeCompetencyId),
  ResourceTypeId int not null,
  constraint FK_ResourceTypeCompetencies_ResourceTypes foreign key (ResourceTypeId) references ResourceTypes(ResourceTypeId) on delete cascade,
  CompetencyId int not null,
  constraint FK_ResourceTypeCompetencies_Competencies foreign key (CompetencyId) references Competencies(CompetencyId) on delete cascade,
  MinimumRequired int not null constraint DF_ResourceTypeCompetencies_MinimumRequired default 1,
  constraint UQ_ResourceTypeCompetencies_ResourceTypeId_CompetencyId unique(ResourceTypeId, CompetencyId),
)
