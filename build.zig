const std = @import("std");
const W = std.unicode.utf8ToUtf16LeStringLiteral;

// Although this function looks imperative, note that its job is to
// declaratively construct a build graph that will be executed by an external
// runner.
pub fn build(b: *std.Build) !void {
    // Standard target options allows the person running `zig build` to choose
    // what target to build for. Here we do not override the defaults, which
    // means any target is allowed, and the default is native. Other options
    // for restricting supported target set are available.
    const target = b.standardTargetOptions(.{});

    // Standard optimization options allow the person running `zig build` to select
    // between Debug, ReleaseSafe, ReleaseFast, and ReleaseSmall. Here we do not
    // set a preferred release mode, allowing the user to decide how to optimize.
    const optimize = b.standardOptimizeOption(.{});

    const lib = b.addSharedLibrary(.{
        .name = "verify",
        // In this case the main source file is merely a path, however, in more
        // complicated build scripts, this could be a generated file.
        .root_source_file = .{ .path = "src/verify.cpp" },
        .target = target,
        .optimize = optimize,
    });
    if (optimize == .ReleaseSafe) {
        lib.want_lto = false;
    }
    lib.linkLibCpp();
    lib.addCSourceFiles(.{
        .files = &[_][]const u8 {
            "./src/canny.cpp"
        }
    });

    // This declares intent for the library to be installed into the standard
    // location when the user invokes the "install" step (the default step when
    // running `zig build`).
    b.installArtifact(lib);

    const exe = b.addExecutable(.{
        .name = "test",
        .root_source_file = .{.path = "./src/main.cpp"},
        .target = target,
        .optimize = optimize
    });
    if (optimize == .ReleaseSafe) {
        exe.want_lto = false;
    }
    exe.addCSourceFiles(.{
        .files = &[_][]const u8 {
            "./src/verify.cpp",
            "./src/canny.cpp",
        }
    });
    exe.linkLibCpp();
    exe.addLibraryPath(.{.path = "D:/Softwares/Program_Files/C/lib"});
    exe.linkSystemLibrary2("opencv_world460.dll", .{.use_pkg_config = .no});
    b.installArtifact(exe);

    const run_cmd = b.addRunArtifact(exe);
    run_cmd.step.dependOn(b.getInstallStep());
    if (b.args) |args| {
        run_cmd.addArgs(args);
    }
    const run_step = b.step("run", "run");
    run_step.dependOn(&run_cmd.step);

    var cwd = std.fs.cwd();
    defer cwd.close();
    const file: ?std.fs.File = cwd.openFileW(
        W("compile_flags.txt"),
        .{.mode = .read_only}
    ) catch null;
    if (file) |f| {
        defer f.close();
        var buffer: [500]u8 = undefined;
        const size = try f.readAll(&buffer);
        var iter = std.mem.splitSequence(u8, buffer[0..size], "\n");
        while (iter.next()) |line| {
            if (line.len > 2) {
                switch (line[1]) {
                    'I' => {
                        lib.addIncludePath(std.build.LazyPath{.path = line[2..]});
                        exe.addIncludePath(std.build.LazyPath{.path = line[2..]});
                    },
                    'L' => {
                        lib.addLibraryPath(std.build.LazyPath{.path = line[2..]});
                        exe.addLibraryPath(std.build.LazyPath{.path = line[2..]});
                    },
                    'l' => {
                        lib.linkSystemLibraryName(line[2..]);
                        exe.linkSystemLibraryName(line[2..]);
                    },
                    else => unreachable,
                }
            }
        }
    }
}
