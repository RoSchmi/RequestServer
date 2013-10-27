#include "Objects.h"

using namespace game_server;
using namespace game_server::objects;

data_object::data_object(uint8 obj_type) {
	this->obj_type = obj_type;
	this->owner = 0;
}

data_object::~data_object() {

}

map_object::map_object(uint8 obj_type) : data_object(obj_type) {
	this->last_updated = date_time::clock::now();

	this->width = 1;
	this->height = 1;
	this->planet_id = 0;
	this->x = 0;
	this->y = 0;
}

map_object::~map_object() {

}
