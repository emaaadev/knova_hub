using System.IdentityModel.Tokens.Jwt;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Pruebas unitarias de AuthService.RegisterAsync. El repositorio se simula con Moq,
/// así que no se necesita base de datos.
/// </summary>
[TestFixture]
public class AuthServiceRegisterTests
{
    private Mock<IUserRepository> _repo = null!;
    private AuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<IUserRepository>();
        _service = new AuthService(_repo.Object, TestData.JwtSettings());
    }

    private void RepositorioDevuelveLoRecibido()
    {
        _repo.Setup(r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()))
            .ReturnsAsync((Company company, User admin) =>
            {
                admin.Id = 1;
                admin.Company = company;
                admin.Role = new Role { Id = SystemRoles.AdminEmpresaRoleId, Name = SystemRoles.AdminEmpresaRoleName };
                return admin;
            });
    }

    [Test]
    public void Register_ContrasenasNoCoinciden_LanzaDataValidationException()
    {
        var dto = TestData.RegistroValido();
        dto.ConfirmPassword = "Distinta123!";

        var ex = Assert.ThrowsAsync<DataValidationException>(() => _service.RegisterAsync(dto));

        Assert.That(ex!.Message, Does.Contain("no coinciden"));
        _repo.Verify(r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()), Times.Never);
    }

    [TestCase("Corta1!", TestName = "Register_ContrasenaMenorDe8Caracteres_Rechazada")]
    [TestCase("SinNumero!", TestName = "Register_ContrasenaSinNumero_Rechazada")]
    [TestCase("SinEspecial1", TestName = "Register_ContrasenaSinCaracterEspecial_Rechazada")]
    [TestCase("12345678", TestName = "Register_ContrasenaSoloNumeros_Rechazada")]
    [TestCase("Espacio 123", TestName = "Register_ContrasenaConEspacioComoUnicoEspecial_Rechazada")]
    public void Register_ContrasenaDebil_LanzaDataValidationException(string password)
    {
        var dto = TestData.RegistroValido();
        dto.Password = password;
        dto.ConfirmPassword = password;

        var ex = Assert.ThrowsAsync<DataValidationException>(() => _service.RegisterAsync(dto));

        Assert.That(ex!.Message, Does.Contain("mínimo 8 caracteres"));
        _repo.Verify(r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task Register_DatosValidos_NormalizaDatosYCreaAdminConHash()
    {
        var dto = TestData.RegistroValido();
        dto.Email = "  Contacto@EMPRESA.com ";
        dto.Rnc = " 131234567 ";
        dto.CompanyName = "  Empresa de Prueba SRL  ";

        Company? companyGuardada = null;
        User? adminGuardado = null;
        _repo.Setup(r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()))
            .Callback<Company, User>((c, u) => { companyGuardada = c; adminGuardado = u; })
            .ReturnsAsync((Company c, User u) => { u.Company = c; return u; });

        await _service.RegisterAsync(dto);

        Assert.Multiple(() =>
        {
            Assert.That(companyGuardada!.Rnc, Is.EqualTo("131234567"));
            Assert.That(companyGuardada.Name, Is.EqualTo("Empresa de Prueba SRL"));
            Assert.That(companyGuardada.Email, Is.EqualTo("contacto@empresa.com"));
            Assert.That(adminGuardado!.Email, Is.EqualTo("contacto@empresa.com"));
            Assert.That(adminGuardado.RoleId, Is.EqualTo(SystemRoles.AdminEmpresaRoleId));
            Assert.That(adminGuardado.PasswordHash, Is.Not.EqualTo(TestData.PasswordValida),
                "La contraseña nunca debe guardarse en texto plano");
            Assert.That(BCrypt.Net.BCrypt.Verify(TestData.PasswordValida, adminGuardado.PasswordHash), Is.True);
        });
    }

    [Test]
    public async Task Register_DatosValidos_DevuelveTokenYDatosDeLaEmpresa()
    {
        RepositorioDevuelveLoRecibido();

        var respuesta = await _service.RegisterAsync(TestData.RegistroValido());

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.Token, Is.Not.Empty);
            Assert.That(new JwtSecurityTokenHandler().CanReadToken(respuesta.Token), Is.True);
            Assert.That(respuesta.Role, Is.EqualTo(SystemRoles.AdminEmpresaRoleName));
            Assert.That(respuesta.CompanyName, Is.EqualTo("Empresa de Prueba SRL"));
            Assert.That(respuesta.Rnc, Is.EqualTo("131234567"));
        });
    }

    [Test]
    public void Register_RncDuplicado_PropagaDuplicateEntityException()
    {
        _repo.Setup(r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()))
            .ThrowsAsync(new DuplicateEntityException("rnc", "Ese RNC ya está registrado."));

        var ex = Assert.ThrowsAsync<DuplicateEntityException>(() => _service.RegisterAsync(TestData.RegistroValido()));

        Assert.That(ex!.Field, Is.EqualTo("rnc"));
    }

    [TestCase("12345", TestName = "Register_RncConMenosDe9Digitos_Rechazado")]
    [TestCase("1234567890", TestName = "Register_RncConMasDe9Digitos_Rechazado")]
    [TestCase("ABC123456", TestName = "Register_RncConLetras_Rechazado")]
    [TestCase("12345678A", TestName = "Register_RncConCaracterEspecial_Rechazado")]
    public void Register_RncInvalido_LanzaDataValidationException(string rnc)
    {
        var dto = TestData.RegistroValido();
        dto.Rnc = rnc;

        var ex = Assert.ThrowsAsync<DataValidationException>(
            () => _service.RegisterAsync(dto));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Field, Is.EqualTo("rnc"));
            Assert.That(ex.Message, Does.Contain("9 dígitos"));
        });

        _repo.Verify(
            r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()),
            Times.Never);
    }

    [TestCase("correo-invalido", TestName = "Register_EmailSinArroba_Rechazado")]
    [TestCase("correo@", TestName = "Register_EmailSinDominio_Rechazado")]
    [TestCase("@empresa.com", TestName = "Register_EmailSinUsuario_Rechazado")]
    public void Register_EmailInvalido_LanzaDataValidationException(string email)
    {
        var dto = TestData.RegistroValido();
        dto.Email = email;

        var ex = Assert.ThrowsAsync<DataValidationException>(
            () => _service.RegisterAsync(dto));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Field, Is.EqualTo("email"));
            Assert.That(ex.Message, Does.Contain("formato válido"));
        });

        _repo.Verify(
            r => r.RegisterCompanyAsync(It.IsAny<Company>(), It.IsAny<User>()),
            Times.Never);
    }

}
