#include "Objects.h"

using namespace game_server;
using namespace game_server::objects;

cached_object::cached_object() {
	this->last_updated_by_cache = date_time::clock::now();
}

cached_object::~cached_object() {

}

map_object::map_object(uint8 obj_type) : object(obj_type) {
	this->width = 1;
	this->height = 1;
	this->planet_id = 0;
	this->x = 0;
	this->y = 0;
}

map_object::~map_object() {

}
