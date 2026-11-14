using KnovaHub.API.Filters;
using KnovaHub.DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Códigos HTTP que devuelve KnovaExceptionFilter para los errores de la IA.
/// </summary>
[TestFixture]
public class KnovaExceptionFilterAiTests
{
    private static ExceptionContext Contexto(Exception ex) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>())
        {
            Exception = ex
        };

    // Lee la propiedad "message" del cuerpo de la respuesta (un objeto anónimo { message }).
    private static string? Mensaje(object? cuerpo) =>
        cuerpo?.GetType().GetProperty("message")?.GetValue(cuerpo) as string;

    [Test]
    public void AiRateLimit_Responde429ConElMensaje()
    {
        var contexto = Contexto(new AiRateLimitException("Límite alcanzado."));

        new KnovaExceptionFilter().OnException(contexto);

        var resultado = contexto.Result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.Not.Null);
            Assert.That(resultado!.StatusCode, Is.EqualTo(429));
            Assert.That(Mensaje(resultado.Value), Is.EqualTo("Límite alcanzado."));
            Assert.That(contexto.ExceptionHandled, Is.True);
        });
    }

    [TestCase(502, TestName = "AiService_Con502_Responde502")]
    [TestCase(503, TestName = "AiService_Con503_Responde503")]
    [TestCase(504, TestName = "AiService_Con504_Responde504")]
    public void AiService_RespondeConSuPropioCodigo(int codigo)
    {
        var contexto = Contexto(new AiServiceException("La IA no está disponible.", codigo));

        new KnovaExceptionFilter().OnException(contexto);

        var resultado = contexto.Result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.Not.Null);
            Assert.That(resultado!.StatusCode, Is.EqualTo(codigo));
            Assert.That(Mensaje(resultado.Value), Is.EqualTo("La IA no está disponible."));
            Assert.That(contexto.ExceptionHandled, Is.True);
        });
    }

    [Test]
    public void ExcepcionDesconocida_NoSeManeja()
    {
        var contexto = Contexto(new InvalidOperationException("otro error"));

        new KnovaExceptionFilter().OnException(contexto);

        Assert.Multiple(() =>
        {
            Assert.That(contexto.Result, Is.Null);
            Assert.That(contexto.ExceptionHandled, Is.False);
        });
    }
}