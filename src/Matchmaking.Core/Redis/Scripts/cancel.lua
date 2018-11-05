-- KEYS[1] tickets hash, KEYS[2] region queue
-- ARGV[1] player id, ARGV[2] region
local ticket = redis.call('HGET', KEYS[1], ARGV[1])
if not ticket or string.sub(ticket, 1, #ARGV[2] + 1) ~= ARGV[2] .. '|' then
    return 0
end

redis.call('HDEL', KEYS[1], ARGV[1])
redis.call('ZREM', KEYS[2], ARGV[1])
return 1
