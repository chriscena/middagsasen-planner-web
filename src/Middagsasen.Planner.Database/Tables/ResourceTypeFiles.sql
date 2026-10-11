CREATE TABLE [dbo].[ResourceTypeFiles]
(
  ResourceTypeFileId int not null identity,
  constraint PK_ResourceTypeFiles PRIMARY key (ResourceTypeFileId),
  StorageName nvarchar(400) not null,
  FileName nvarchar(400) not null,
  Description nvarchar(1000) not null,
  MimeType nvarchar(400) not null,
  ResourceTypeId int not null,
  constraint FK_ResourceTypeFiles_ResourceTypes foreign key (ResourceTypeId) references ResourceTypes(ResourceTypeId),
  Created datetime not null,
  CreatedBy int not null,
  constraint FK_ResourceTypeFiles_Users_CreatedBy foreign key (CreatedBy) references Users(UserId),
  Updated datetime not null,
  UpdatedBy int not null,
   constraint FK_ResourceTypeFiles_Users_UpdatedBy foreign key (UpdatedBy) references Users(UserId),
  -- Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.
  RequiresAcknowledgement bit not null constraint DF_ResourceTypeFiles_RequiresAcknowledgement default 0,
  -- Instruksen er påkrevd: brukeren må ha kvittert før egen påmelding på vakttypen. Krever RequiresAcknowledgement.
  IsMandatory bit not null constraint DF_ResourceTypeFiles_IsMandatory default 0,
  -- Arkivert vedlegg (UTC): fjernet fra vakttypen, men beholdt for historikk og kvitteringsrapport. Slettes aldri fysisk.
  Archived datetime null,
  ArchivedBy int null,
  constraint FK_ResourceTypeFiles_Users_ArchivedBy foreign key (ArchivedBy) references Users(UserId),
)
