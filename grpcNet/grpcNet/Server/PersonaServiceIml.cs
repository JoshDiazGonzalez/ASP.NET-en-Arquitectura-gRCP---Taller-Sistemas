using Grpc.Core;
using Vaxi;
using static Vaxi.PersonaService;

namespace Server
{
    public class PersonaServiceIml : PersonaServiceBase
    {
        public override Task<PersonaResponse> RegistrarPersona(PersonaRequest request, ServerCallContext context) 
        {

            string mensaje = $"Se insertó correctamente el usuario: {request.Persona.Nombre} - {request.Persona.Email}";

            PersonaResponse response = new PersonaResponse
            {
                Mensaje = mensaje
            };

            

            return Task.FromResult(response);
        }
    }
}
