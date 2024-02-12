
-- Globals declared here
PI = math.pi            -- 3.1415926535897932384626433832795028841971693993751
HPI = PI/2                -- Radians in 90Â°
PI2 = PI*2                -- Radians in 360Â°
r2d = 180/PI            -- Radians to Degrees
told = 0.0            -- Last Time function called


Myfunction =
{

AfterNextFrame=function(self)
	local fmData = LoGetHelicopterFMData()

	if fmData then
	    -- Gファクターを取得
	    local gFactorX = fmData.G_factor.x
	    local gFactorY = fmData.G_factor.y
	    local gFactorZ = fmData.G_factor.z

	    -- 速度を取得
	    local speedX = fmData.speed.x
	    local speedY = fmData.speed.y
	    local speedZ = fmData.speed.z

	    -- 加速度を取得
	    local accelerationX = fmData.acceleration.x
	    local accelerationY = fmData.acceleration.y
	    local accelerationZ = fmData.acceleration.z

	    -- 角速度を取得
	    local angularSpeedX = fmData.angular_speed.x
	    local angularSpeedY = fmData.angular_speed.y
	    local angularSpeedZ = fmData.angular_speed.z

	    -- 角加速度を取得
	    local angularAccelX = fmData.angular_acceleration.x
	    local angularAccelY = fmData.angular_acceleration.y
	    local angularAccelZ = fmData.angular_acceleration.z

	    -- ヨー、ピッチ、ロールを取得
	    local yaw = fmData.yaw * r2d
	    local pitch = fmData.pitch * r2d
	    local roll = fmData.roll * r2d
	    socket.try(MySocket:send(string.format("[P]%.3f[Y]%.3f[R]%.3f[GFX]%.3f[GFY]%.3f[GFZ]%.3f[AAX]%.3f[AAY]%.3f[AAZ]%.3f\n", pitch, yaw, roll, gFactorX, gFactorY, gFactorZ,angularAccelX, angularAccelY, angularAccelZ )))
	end
end


}



function LuaExportStart()

	package.path  = package.path..";"..lfs.currentdir().."/LuaSocket/?.lua"
	package.cpath = package.cpath..";"..lfs.currentdir().."/LuaSocket/?.dll"

	socket = require("socket")
	IPAddress = "127.0.0.1"
	Port = 31090

	MySocket = socket.try(socket.connect(IPAddress, Port))
	MySocket:setoption("tcp-nodelay",true) 
end

function LuaExportBeforeNextFrame()
end

function LuaExportAfterNextFrame()

	local IAS = LoGetIndicatedAirSpeed()
	
	-- socket.try(MySocket:send(string.format("IAS: %.4f \n",IAS)))
	
    Myfunction:AfterNextFrame()
end

function LuaExportStop()

	if MySocket then 
		socket.try(MySocket:send("exit"))
		MySocket:close()
	end
end

function LuaExportActivityNextEvent(t)
end