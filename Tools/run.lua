local aliases = {
    SetStandaloneFileCommented = {"sfc", "fc", "setsfc"},
    ThickenOutlineIcons = {"toi", "thick", "icons"}
}

local command = arg[1]
local argsStr = table.concat(arg, " ", 2)

local function runLuaCommand(fileNameNoExt)
    local cmdString = "lua Tools/"..fileNameNoExt..".lua "..argsStr.." > temp.txt"
    local success, exit_type, code = os.execute(cmdString)

    if success then
        -- Open and read the file
        local file = io.open("temp.txt", "r")
        if file == nil then
            warn("Could not open temp.txt for output but command was successful")
            return
        end

        local output = file:read("*a")
        file:close()

        os.remove("temp.txt")

        print("Command succeeded. Output: " .. output)
    else
        print("Command failed with code: " .. tostring(code))
    end
end

if aliases[command] then
    runLuaCommand(command)
    return
end

for cmd, cmdaliases in pairs(aliases) do
    for _, v in pairs(cmdaliases) do
        if v == command then
            runLuaCommand(cmd)
            return
        end
    end
end

error("Could not find command for alias "..command)