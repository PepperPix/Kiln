namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class OpenApiDocGeneratorTests
{
    private const string MinimalSpec = """
        openapi: "3.0.0"
        info:
          title: "Test API"
          version: "1.0.0"
        paths:
          /pets:
            get:
              tags: ["pets"]
              operationId: "listPets"
              summary: "List all pets"
              parameters:
                - name: limit
                  in: query
                  required: false
                  schema:
                    type: integer
              responses:
                "200":
                  description: "A list of pets"
          /pets/{petId}:
            get:
              tags: ["pets"]
              operationId: "getPet"
              summary: "Get a pet by ID"
              parameters:
                - name: petId
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                "200":
                  description: "A pet"
                "404":
                  description: "Not found"
          /owners:
            post:
              tags: ["owners"]
              operationId: "createOwner"
              summary: "Create an owner"
              requestBody:
                content:
                  application/json:
                    schema:
                      properties:
                        name:
                          type: string
                        email:
                          type: string
              responses:
                "201":
                  description: "Created"
        """;

    [Test]
    public async Task Generate_WithValidSpec_CreatesExpectedFilesWithCorrectFrontmatter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-docs-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");

        try
        {
            await File.WriteAllTextAsync(specPath, MinimalSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            var report = generator.Generate(specPath, tempDir);

            await Assert.That(report.Warnings).IsEmpty();
            await Assert.That(report.Conflicts).IsEmpty();
            await Assert.That(report.Skipped).IsEmpty();

            // Expect 3 operations: listPets, getPet, createOwner
            const int expectedOperationCount = 3;
            await Assert.That(report.Written.Count).IsEqualTo(expectedOperationCount);

            // Check listPets file
            var listPetsPath = Path.Combine(tempDir, "pets", "listpets.md");
            await Assert.That(File.Exists(listPetsPath)).IsTrue();
            var listPetsContent = await File.ReadAllTextAsync(listPetsPath);
            await Assert.That(listPetsContent).Contains("title: List all pets");
            await Assert.That(listPetsContent).Contains("generated: true");
            await Assert.That(listPetsContent).Contains("source_hash:");
            await Assert.That(listPetsContent).Contains("`GET /pets`");
            await Assert.That(listPetsContent).Contains("## Parameters");
            await Assert.That(listPetsContent).Contains("limit");
            await Assert.That(listPetsContent).Contains("## Responses");
            await Assert.That(listPetsContent).Contains("200");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }

    [Test]
    public async Task Generate_WithMultipleTags_CreatesSubdirectoriesPerTag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-tags-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");

        try
        {
            await File.WriteAllTextAsync(specPath, MinimalSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            var report = generator.Generate(specPath, tempDir);
            await Assert.That(report.Warnings).IsEmpty();

            await Assert.That(Directory.Exists(Path.Combine(tempDir, "pets"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(tempDir, "owners"))).IsTrue();

            await Assert.That(File.Exists(Path.Combine(tempDir, "pets", "listpets.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "pets", "getpet.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "owners", "createowner.md"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }

    [Test]
    public async Task Generate_RequestBody_IsIncludedInBody()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-body-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");

        try
        {
            await File.WriteAllTextAsync(specPath, MinimalSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            generator.Generate(specPath, tempDir);

            var createOwnerPath = Path.Combine(tempDir, "owners", "createowner.md");
            var content = await File.ReadAllTextAsync(createOwnerPath);
            await Assert.That(content).Contains("## Request Body");
            await Assert.That(content).Contains("application/json");
            await Assert.That(content).Contains("name");
            await Assert.That(content).Contains("email");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }

    [Test]
    public async Task Generate_WithJsonSpec_ProducesEquivalentMarkdown()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-json-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.json");
        const string jsonSpec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "JSON API", "version": "1.0.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "tags": ["pets"],
                    "operationId": "listPetsJson",
                    "summary": "List all pets",
                    "responses": { "200": { "description": "OK" } }
                  }
                }
              }
            }
            """;

        try
        {
            await File.WriteAllTextAsync(specPath, jsonSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            var report = generator.Generate(specPath, tempDir);
            await Assert.That(report.Warnings).IsEmpty();
            await Assert.That(report.Written).Contains("pets/listpetsjson.md");

            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "pets", "listpetsjson.md"));
            await Assert.That(content).Contains("`GET /pets`");
            await Assert.That(content).Contains("List all pets");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }

    [Test]
    public async Task Generate_WithNullableSchema_OmitsNullTypeFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-nullable-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");
        const string nullableSpec = """
            openapi: "3.0.0"
            info:
              title: "Nullable API"
              version: "1.0.0"
            paths:
              /pets:
                get:
                  tags: ["pets"]
                  operationId: "findPet"
                  summary: "Find a pet"
                  parameters:
                    - name: petName
                      in: query
                      required: false
                      schema:
                        type: string
                        nullable: true
                  responses:
                    "200":
                      description: "OK"
            """;

        try
        {
            await File.WriteAllTextAsync(specPath, nullableSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            var report = generator.Generate(specPath, tempDir);
            await Assert.That(report.Warnings).IsEmpty();

            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "pets", "findpet.md"));
            await Assert.That(content).Contains("| petName | query | no | string |");
            await Assert.That(content).DoesNotContain("| petName | query | no | null |");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }

    [Test]
    public async Task Generate_OrdersOperationsByHttpMethodPriority()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gen-order-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");
        const string orderSpec = """
            openapi: "3.0.0"
            info:
              title: "Order API"
              version: "1.0.0"
            paths:
              /pets:
                trace:
                  tags: ["pets"]
                  operationId: "tracePets"
                  responses:
                    "200":
                      description: "OK"
                head:
                  tags: ["pets"]
                  operationId: "headPets"
                  responses:
                    "200":
                      description: "OK"
                patch:
                  tags: ["pets"]
                  operationId: "patchPets"
                  responses:
                    "200":
                      description: "OK"
                delete:
                  tags: ["pets"]
                  operationId: "deletePets"
                  responses:
                    "200":
                      description: "OK"
                options:
                  tags: ["pets"]
                  operationId: "optionsPets"
                  responses:
                    "200":
                      description: "OK"
                post:
                  tags: ["pets"]
                  operationId: "createPet"
                  responses:
                    "200":
                      description: "OK"
                put:
                  tags: ["pets"]
                  operationId: "updatePet"
                  responses:
                    "200":
                      description: "OK"
                get:
                  tags: ["pets"]
                  operationId: "listPets"
                  responses:
                    "200":
                      description: "OK"
            """;

        try
        {
            await File.WriteAllTextAsync(specPath, orderSpec);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new OpenApiDocGenerator(writer);

            var report = generator.Generate(specPath, tempDir);
            await Assert.That(report.Warnings).IsEmpty();
            await Assert.That(report.Written.Count).IsEqualTo(8);
            await Assert.That(report.Written[0]).IsEqualTo("pets/listpets.md");
            await Assert.That(report.Written[1]).IsEqualTo("pets/updatepet.md");
            await Assert.That(report.Written[2]).IsEqualTo("pets/createpet.md");
            await Assert.That(report.Written[3]).IsEqualTo("pets/deletepets.md");
            await Assert.That(report.Written[4]).IsEqualTo("pets/optionspets.md");
            await Assert.That(report.Written[5]).IsEqualTo("pets/headpets.md");
            await Assert.That(report.Written[6]).IsEqualTo("pets/patchpets.md");
            await Assert.That(report.Written[7]).IsEqualTo("pets/tracepets.md");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }
}
