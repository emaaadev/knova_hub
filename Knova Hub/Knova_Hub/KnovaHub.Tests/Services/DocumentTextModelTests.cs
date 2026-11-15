using KnovaHub.DomainLayer.Entities;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Revisa la configuración de EF para el texto de los documentos. Construir el modelo
/// no abre conexión, así que no hace falta SQL Server.
/// </summary>
[TestFixture]
public class DocumentTextModelTests
{
    private IModel _model = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var options = new DbContextOptionsBuilder<KnovaHubDbContext>()
            .UseSqlServer("Server=no-se-usa;Database=no-se-usa")
            .Options;
        using var context = new KnovaHubDbContext(options);
        // El modelo de diseño es el que usan las migraciones e incluye las restricciones CHECK.
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Test]
    public void ElMarkdownVaEnSuPropiaTabla_NoEnDocuments()
    {
        var texto = _model.FindEntityType(typeof(DocumentText))!;
        var documento = _model.FindEntityType(typeof(Document))!;

        Assert.Multiple(() =>
        {
            Assert.That(texto.GetTableName(), Is.EqualTo("DocumentTexts"));
            Assert.That(documento.FindProperty(nameof(DocumentText.Markdown)), Is.Null,
                "Los listados de documentos no deben cargar el texto completo");
        });
    }

    [Test]
    public void ElTextoPerteneceAUnDocumentoDeLaMismaEmpresaYSeBorraConEl()
    {
        var fk = _model.FindEntityType(typeof(DocumentText))!.GetForeignKeys().Single();

        Assert.Multiple(() =>
        {
            Assert.That(fk.Properties.Select(p => p.Name),
                Is.EqualTo(new[] { nameof(DocumentText.DocumentId), nameof(DocumentText.CompanyId) }));
            Assert.That(fk.PrincipalKey.Properties.Select(p => p.Name),
                Is.EqualTo(new[] { nameof(Document.Id), nameof(Document.CompanyId) }));
            Assert.That(fk.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(fk.IsUnique, Is.True, "Un documento tiene como máximo un texto");
        });
    }

    [Test]
    public void DocumentsTieneLaRestriccionDeEstadoDeTexto()
    {
        var restriccion = _model.FindEntityType(typeof(Document))!
            .GetCheckConstraints()
            .Single(c => c.Name == "CK_Documents_TextStatus");

        // Sin el IS NOT NULL, SQL Server aceptaría un archivo sin estado (NULL IN (...) es UNKNOWN).
        Assert.That(restriccion.Sql, Does.Contain("[TextStatus] IS NOT NULL AND [TextStatus] IN (1, 2, 3)"));
    }
}
