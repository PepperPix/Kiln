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
            var listPetsPath = Path.Combine(tempDir, "pets", "listPets.md");
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

            generator.Generate(specPath, tempDir);

            await Assert.That(Directory.Exists(Path.Combine(tempDir, "pets"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(tempDir, "owners"))).IsTrue();

            await Assert.That(File.Exists(Path.Combine(tempDir, "pets", "listPets.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "pets", "getPet.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "owners", "createOwner.md"))).IsTrue();
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

            var createOwnerPath = Path.Combine(tempDir, "owners", "createOwner.md");
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
}
