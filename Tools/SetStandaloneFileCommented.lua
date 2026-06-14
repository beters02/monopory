local filePath = arg[1]
local toggleString = arg[2]
local toggle
local usage = "SetStandaloneFileCommented.lua {filePath} {toggleBool (0, 1, true, false, True, False)}"

if not filePath then
    print(usage)
    return
end

if not toggleString then
    print(usage)
    error("Must provide filePath and toggleString argument.")
    return
end

if toggleString == "0" then
    toggle = false
elseif toggleString == "1" then
    toggle = true
elseif string.lower(toggleString) == "true" then
    toggle = true
elseif string.lower(toggleString) == "false" then
    toggle = false
else
    print(usage)
    error("Could not parse toggleBool argument: " .. toggleString)
end

local file = io.open(filePath, "rb")
if not file then
    error("Could not open file for reading: " .. filePath)
end

local content = file:read("*a")
file:close()

local function isStandaloneDirective(line)
    return line:match("^%s*#if%f[%A]") or line:match("^%s*#endif%f[%A]")
end

local function isCommentedStandaloneDirective(line)
    return line:match("^%s*//%s*#if%f[%A]") or line:match("^%s*//%s*#endif%f[%A]")
end

local function processLine(line)
    if toggle then
        if isCommentedStandaloneDirective(line) or not isStandaloneDirective(line) then
            return line
        end

        return line:gsub("^(%s*)(#if%f[%A])", "%1//%2", 1)
            :gsub("^(%s*)(#endif%f[%A])", "%1//%2", 1)
    end

    if not isCommentedStandaloneDirective(line) then
        return line
    end

    return line:gsub("^(%s*)//%s*(#if%f[%A])", "%1%2", 1)
        :gsub("^(%s*)//%s*(#endif%f[%A])", "%1%2", 1)
end

local output = {}
local index = 1

while index <= #content do
    local lineEnd = content:find("[\r\n]", index)
    local line
    local newline = ""

    if lineEnd then
        line = content:sub(index, lineEnd - 1)
        if content:sub(lineEnd, lineEnd + 1) == "\r\n" then
            newline = "\r\n"
            index = lineEnd + 2
        else
            newline = content:sub(lineEnd, lineEnd)
            index = lineEnd + 1
        end
    else
        line = content:sub(index)
        index = #content + 1
    end

    table.insert(output, processLine(line) .. newline)
end

local outputFile = io.open(filePath, "wb")
if not outputFile then
    error("Could not open file for writing: " .. filePath)
end

outputFile:write(table.concat(output))
outputFile:close()

if toggle then
    print("Successfully commented out Standalone")
else
    print("Successfully uncommented Standalone")
end
