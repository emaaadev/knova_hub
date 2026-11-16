using System.Text;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Pruebas de cómo DocumentService guarda, reemplaza y borra el Markdown del adjunto.
/// El extractor, el almacenamiento y los repositorios se simulan.
/// </summary>
[TestFixture]
public class DocumentServiceTextTests
{
    private const int CompanyId = 5;
    private const int UserId = 10;
    private const int DocumentId = 42;

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 contenido de prueba");

    private Mock<IDocumentRepository> _documents = null!;
    private Mock<IFileStorage> _storage = null!;
    private Mock<IDocumentTextExtractor> _extractor = null!;
    private DocumentService _service = null!;
    private Document _document = null!;
    private readonly Actor _admin = new(UserId, CompanyId, SystemRoles.AdminEmpresaRoleName);

    [SetUp]
    public void SetUp()
    {
        _documents = new Mock<IDocumentRepository>();
        _storage = new Mock<IFileStorage>();
        _extractor = new Mock<IDocumentTextExtractor>();
        var users = new Mock<IUserRepository>();

        users.Setup(u => u.GetByIdInCompanyAsync(CompanyId, UserId))
            .ReturnsAsync(new User { Id = UserId, IsActive = true, RoleId = SystemRoles.AdminEmpresaRoleId });

        _document = new Document
        {
            Id = DocumentId,
            CompanyId = CompanyId,
            Title = "Manual",
            Visibility = DocumentVisibility.General
        };
        _documents.Setup(d => d.GetByIdAsync(CompanyId, DocumentId)).ReturnsAsync(() => _document);
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("clave-nueva");

        _service = new DocumentService(_documents.Object, Mock.Of<ICategoryRepository>(),
            Mock.Of<ITagRepository>(), Mock.Of<IDepartmentRepository>(), users.Object,
            _storage.Object, _extractor.Object, Mock.Of<IQueryLogRepository>(), TimeProvider.System);
    }

    private static UploadDocumentFileDto Archivo(string nombre = "manual.pdf", byte[]? bytes = null)
    {
        bytes ??= PdfBytes;
        return new UploadDocumentFileDto
        {
            FileName = nombre,
            ContentType = "application/pdf",
            FileSizeBytes = bytes.Length,
            Content = new MemoryStream(bytes)
        };
    }

    private void ExtractorDevuelve(DocumentTextResult resultado) =>
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);

    [Test]
    public async Task Subir_PdfConTexto_GuardaElMarkdownYQuedaIndexado()
    {
        ExtractorDevuelve(DocumentTextResult.Indexed("# Manual\n\nTexto"));
        DocumentText? guardado = null;
        _documents.Setup(d => d.UpdateWithTextAsync(It.IsAny<Document>(), It.IsAny<DocumentText?>()))
            .Callback<Document, DocumentText?>((_, t) => guardado = t);

        var dto = await _service.UploadFileAsync(_admin, DocumentId, Archivo());

        Assert.Multiple(() =>
        {
            Assert.That(guardado, Is.Not.Null);
            Assert.That(guardado!.Markdown, Is.EqualTo("# Manual\n\nTexto"));
            Assert.That(guardado.CharacterCount, Is.EqualTo("# Manual\n\nTexto".Length));
            Assert.That(guardado.DocumentId, Is.EqualTo(DocumentId));
            Assert.That(guardado.CompanyId, Is.EqualTo(CompanyId));
            Assert.That(dto.TextStatus, Is.EqualTo(DocumentTextStatus.Indexado));
            Assert.That(dto.TextStatusReason, Is.Null);
        });
    }

    [Test]
    public async Task Subir_PdfSinTexto_SeAdjuntaSinErrorYQuedaNoIndexableConElMotivo()
    {
        ExtractorDevuelve(DocumentTextResult.NotIndexable("El PDF parece escaneado."));

        var dto = await _service.UploadFileAsync(_admin, DocumentId, Archivo());

        Assert.Multiple(() =>
        {
            Assert.That(dto.HasFile, Is.True, "El archivo se adjunta aunque la IA no pueda leerlo");
            Assert.That(dto.TextStatus, Is.EqualTo(DocumentTextStatus.NoIndexable));
            Assert.That(dto.TextStatusReason, Is.EqualTo("El PDF parece escaneado."));
        });
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), ".pdf", It.IsAny<CancellationToken>()), Times.Once);
        _documents.Verify(d => d.UpdateWithTextAsync(_document, null), Times.Once);
    }

    [Test]
    public async Task Subir_ElExtractorYElAlmacenamientoRecibenElArchivoCompleto()
    {
        byte[]? leidoPorExtractor = null;
        byte[]? guardadoEnDisco = null;
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), ".pdf", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>((s, _, _) => leidoPorExtractor = Leer(s))
            .ReturnsAsync(DocumentTextResult.Indexed("texto"));
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), ".pdf", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>((s, _, _) => guardadoEnDisco = Leer(s))
            .ReturnsAsync("clave-nueva");

        await _service.UploadFileAsync(_admin, DocumentId, Archivo());

        // Si el extractor dejara el stream al final, se guardaría un archivo vacío.
        Assert.That(leidoPorExtractor, Is.EqualTo(PdfBytes));
        Assert.That(guardadoEnDisco, Is.EqualTo(PdfBytes));
    }

    [Test]
    public async Task Reemplazar_ArchivoIndexadoPorUnoSinTexto_BorraElTextoAnteriorYElArchivoViejo()
    {
        _document.StorageKey = "clave-vieja";
        _document.OriginalFileName = "viejo.pdf";
        _document.TextStatus = DocumentTextStatus.Indexado;
        ExtractorDevuelve(DocumentTextResult.NotIndexable("El PDF no contiene texto."));

        await _service.UploadFileAsync(_admin, DocumentId, Archivo("nuevo.pdf"));

        Assert.That(_document.TextStatus, Is.EqualTo(DocumentTextStatus.NoIndexable));
        _documents.Verify(d => d.UpdateWithTextAsync(_document, null), Times.Once);
        _storage.Verify(s => s.DeleteAsync("clave-vieja", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Reemplazar_ArchivoNoIndexablePorUnoConTexto_QuedaIndexadoYSinMotivo()
    {
        _document.StorageKey = "clave-vieja";
        _document.OriginalFileName = "escaneado.pdf";
        _document.TextStatus = DocumentTextStatus.NoIndexable;
        _document.TextStatusReason = "El PDF parece escaneado.";
        ExtractorDevuelve(DocumentTextResult.Indexed("Texto nuevo"));

        var dto = await _service.UploadFileAsync(_admin, DocumentId, Archivo("con-texto.pdf"));

        Assert.Multiple(() =>
        {
            Assert.That(dto.TextStatus, Is.EqualTo(DocumentTextStatus.Indexado));
            Assert.That(dto.TextStatusReason, Is.Null);
        });
        _documents.Verify(d => d.UpdateWithTextAsync(_document,
            It.Is<DocumentText>(t => t.Markdown == "Texto nuevo")), Times.Once);
    }

    [Test]
    public void Subir_SiFallaElGuardado_SeBorraElArchivoNuevoYNoQuedaTextoHuerfano()
    {
        ExtractorDevuelve(DocumentTextResult.Indexed("texto"));
        _documents.Setup(d => d.UpdateWithTextAsync(It.IsAny<Document>(), It.IsAny<DocumentText?>()))
            .ThrowsAsync(new InvalidOperationException("fallo de base de datos"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadFileAsync(_admin, DocumentId, Archivo()));

        // El texto y el archivo se guardan en la misma transacción: si falla, se limpia el archivo nuevo.
        _storage.Verify(s => s.DeleteAsync("clave-nueva", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Subir_ArchivoInvalido_NoLlegaAlExtractor()
    {
        var dto = Archivo("manual.pdf", Encoding.ASCII.GetBytes("no es un pdf"));

        Assert.ThrowsAsync<KnovaHub.DomainLayer.Exceptions.DataValidationException>(
            () => _service.UploadFileAsync(_admin, DocumentId, dto));

        _extractor.Verify(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task QuitarArchivo_BorraElTextoYElEstado()
    {
        _document.StorageKey = "clave-vieja";
        _document.OriginalFileName = "manual.pdf";
        _document.ContentType = "application/pdf";
        _document.FileExtension = ".pdf";
        _document.FileSizeBytes = 100;
        _document.TextStatus = DocumentTextStatus.Indexado;

        var dto = await _service.DeleteFileAsync(_admin, DocumentId);

        Assert.Multiple(() =>
        {
            Assert.That(dto.TextStatus, Is.Null);
            Assert.That(dto.TextStatusReason, Is.Null);
        });
        _documents.Verify(d => d.UpdateWithTextAsync(_document, null), Times.Once);
        _documents.Verify(d => d.UpdateAsync(It.IsAny<Document>()), Times.Never);
    }

    [Test]
    public async Task EliminarDocumento_DelegaEnElRepositorioQueBorraElTextoEnCascada()
    {
        _document.StorageKey = "clave-vieja";
        _document.TextStatus = DocumentTextStatus.Indexado;

        await _service.DeleteAsync(_admin, DocumentId);

        _documents.Verify(d => d.DeleteAsync(_document), Times.Once);
        _storage.Verify(s => s.DeleteAsync("clave-vieja", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static byte[] Leer(Stream stream)
    {
        using var copia = new MemoryStream();
        stream.CopyTo(copia);
        return copia.ToArray();
    }
}
